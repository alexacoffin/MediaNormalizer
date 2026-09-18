using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;

namespace Application.Normalization.MediaTypes.TV;

internal sealed class TvMediaTypeFormatter
{
    private static readonly Regex YearExpression = new(@"\b(?:18|19|20)\d{2}\b", RegexOptions.CultureInvariant);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly IFileManager fileManager;
    private readonly IImdbClient imdbClient;
    private readonly string outputDirectory;
    private readonly Dictionary<string, Task<ImdbResult<ImdbTitleDetails>>> episodeLookups =
        new(StringComparer.OrdinalIgnoreCase);

    public TvMediaTypeFormatter(
        IFileManager fileManager,
        IImdbClient imdbClient,
        string outputDirectory)
    {
        this.fileManager = fileManager;
        this.imdbClient = imdbClient;
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (!Path.IsPathRooted(outputDirectory))
        {
            throw new ArgumentException(
                "The TV output directory must be an absolute path.",
                nameof(outputDirectory));
        }

        this.outputDirectory = Path.GetFullPath(outputDirectory);
    }

    public async Task<TvMediaTypeFormattingResult> FormatAsync(
        TvIdentificationRunResult identificationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identificationResult);

        var results = new List<TvMediaFileFormattingResult>();
        var directoriesToClean = new HashSet<string>(PathComparer);

        foreach (var identification in identificationResult.ShowIdentifications
            .Concat(identificationResult.LooseFileIdentifications))
        {
            var (tvRootPath, filePaths) = GetFiles(identification);
            if (tvRootPath is null || filePaths is null)
            {
                continue;
            }

            if (identification.Status != TvShowIdentificationStatus.Matched
                || identification.MatchedSeries is null)
            {
                AddSkippedResults(results, filePaths, "The series was not matched with confidence.");
                continue;
            }

            var seriesName = CreateSeriesName(identification.MatchedSeries);
            if (string.IsNullOrWhiteSpace(seriesName))
            {
                AddFailedResults(results, filePaths, "The matched series title cannot be used as a file name.");
                continue;
            }

            foreach (var filePath in filePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var result = await FormatFileAsync(
                    tvRootPath,
                    filePath,
                    identification.MatchedSeries,
                    seriesName,
                    cancellationToken);
                results.Add(result);

                if (result.Status == TvMediaTypeFormattingStatus.Renamed)
                {
                    AddSourceDirectoriesToClean(directoriesToClean, tvRootPath, filePath);
                }
            }
        }

        foreach (var directoryPath in directoriesToClean.OrderByDescending(path => path.Length))
        {
            fileManager.TryDeleteEmptyDirectory(directoryPath);
        }

        return new TvMediaTypeFormattingResult(results.ToArray());
    }

    private async Task<TvMediaFileFormattingResult> FormatFileAsync(
        string tvRootPath,
        string sourceFilePath,
        ImdbTitleSummary matchedSeries,
        string seriesName,
        CancellationToken cancellationToken)
    {
        var candidate = TvFilenameParser.TryParse(sourceFilePath);
        if (candidate is null)
        {
            return Skipped(sourceFilePath, "No supported episode marker was found.");
        }

        if (candidate.IsMultiEpisode)
        {
            return Skipped(sourceFilePath, "Multi-episode files are not supported.");
        }

        var extension = Path.GetExtension(sourceFilePath);
        string destinationDirectory;
        string destinationFileName;
        string message;

        if (candidate.AirDate is not null)
        {
            destinationDirectory = Path.Combine(outputDirectory, seriesName);
            destinationFileName = $"{seriesName} - {candidate.AirDate:yyyy-MM-dd}{extension}";
            message = "Formatted from the air-date marker.";
        }
        else if (candidate.SeasonNumber is not null && candidate.EpisodeNumber is not null)
        {
            destinationDirectory = Path.Combine(outputDirectory, seriesName, $"Season {candidate.SeasonNumber.Value:D2}");
            var episodeCode = $"S{candidate.SeasonNumber.Value:D2}E{candidate.EpisodeNumber.Value:D2}";
            var episodeTitle = await TryGetEpisodeTitleAsync(
                matchedSeries.ImdbId,
                candidate.SeasonNumber.Value,
                candidate.EpisodeNumber.Value,
                cancellationToken);
            destinationFileName = string.IsNullOrWhiteSpace(episodeTitle)
                ? $"{seriesName} - {episodeCode}{extension}"
                : $"{seriesName} - {episodeCode} - {episodeTitle}{extension}";
            message = string.IsNullOrWhiteSpace(episodeTitle)
                ? "Episode metadata was unavailable; formatted without an episode title."
                : "Formatted with matched episode metadata.";
        }
        else
        {
            return Skipped(sourceFilePath, "The episode marker does not include both season and episode numbers.");
        }

        var destinationFilePath = Path.Combine(destinationDirectory, destinationFileName);
        if (PathsEqual(sourceFilePath, destinationFilePath))
        {
            return new TvMediaFileFormattingResult(
                sourceFilePath,
                destinationFilePath,
                TvMediaTypeFormattingStatus.AlreadyNormalized,
                "The file already has the canonical path and name.");
        }

        if (fileManager.FileExists(destinationFilePath))
        {
            return new TvMediaFileFormattingResult(
                sourceFilePath,
                destinationFilePath,
                TvMediaTypeFormattingStatus.Skipped,
                "The destination file already exists.");
        }

        try
        {
            fileManager.EnsureDirectory(destinationDirectory);
            fileManager.MoveFile(sourceFilePath, destinationFilePath);
            return new TvMediaFileFormattingResult(
                sourceFilePath,
                destinationFilePath,
                TvMediaTypeFormattingStatus.Renamed,
                message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failed(sourceFilePath, destinationFilePath, exception.Message);
        }
        catch (DirectoryNotFoundException exception)
        {
            return Failed(sourceFilePath, destinationFilePath, exception.Message);
        }
        catch (FileNotFoundException exception)
        {
            return Failed(sourceFilePath, destinationFilePath, exception.Message);
        }
        catch (IOException exception)
        {
            return Failed(sourceFilePath, destinationFilePath, exception.Message);
        }
    }

    private async Task<string?> TryGetEpisodeTitleAsync(
        string seriesImdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        var lookupKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{seriesImdbId}|{seasonNumber}|{episodeNumber}");
        if (!episodeLookups.TryGetValue(lookupKey, out var lookup))
        {
            lookup = imdbClient.GetEpisodeAsync(seriesImdbId, seasonNumber, episodeNumber, cancellationToken);
            episodeLookups.Add(lookupKey, lookup);
        }

        var episode = await lookup;
        return episode.IsSuccess && !string.IsNullOrWhiteSpace(episode.Value?.Title)
            ? SanitizeFileNameComponent(episode.Value.Title)
            : null;
    }

    private static (string? TvRootPath, string[]? FilePaths) GetFiles(TvShowIdentification identification) =>
        identification.ShowFolderGroup is not null
            ? (identification.ShowFolderGroup.TvRootPath, identification.ShowFolderGroup.FilePaths)
            : identification.LooseFileGroup is not null
                ? (identification.LooseFileGroup.TvRootPath, identification.LooseFileGroup.FilePaths)
                : (null, null);

    private static void AddSkippedResults(
        ICollection<TvMediaFileFormattingResult> results,
        IEnumerable<string> filePaths,
        string message)
    {
        foreach (var filePath in filePaths)
        {
            results.Add(Skipped(filePath, message));
        }
    }

    private static void AddFailedResults(
        ICollection<TvMediaFileFormattingResult> results,
        IEnumerable<string> filePaths,
        string message)
    {
        foreach (var filePath in filePaths)
        {
            results.Add(Failed(filePath, null, message));
        }
    }

    private static TvMediaFileFormattingResult Skipped(string sourceFilePath, string message) =>
        new(sourceFilePath, null, TvMediaTypeFormattingStatus.Skipped, message);

    private static TvMediaFileFormattingResult Failed(
        string sourceFilePath,
        string? destinationFilePath,
        string message) =>
        new(sourceFilePath, destinationFilePath, TvMediaTypeFormattingStatus.Failed, message);

    private static string? CreateSeriesName(ImdbTitleSummary matchedSeries)
    {
        var title = SanitizeFileNameComponent(matchedSeries.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var year = YearExpression.Match(matchedSeries.Year).Value;
        return string.IsNullOrWhiteSpace(year) ? title : $"{title} ({year})";
    }

    private static string? SanitizeFileNameComponent(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) || char.IsControl(character) ? ' ' : character);
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ", RegexOptions.CultureInvariant).Trim(' ', '.');
    }

    private static bool PathsEqual(string left, string right) =>
        PathComparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));

    private static void AddSourceDirectoriesToClean(
        ISet<string> directoriesToClean,
        string tvRootPath,
        string sourceFilePath)
    {
        var rootPath = Path.GetFullPath(tvRootPath);
        var directoryPath = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));

        while (!string.IsNullOrWhiteSpace(directoryPath) && !PathsEqual(directoryPath, rootPath))
        {
            if (!IsChildOf(directoryPath, rootPath))
            {
                return;
            }

            directoriesToClean.Add(directoryPath);
            directoryPath = Path.GetDirectoryName(directoryPath);
        }
    }

    private static bool IsChildOf(string path, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        return !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }
}
