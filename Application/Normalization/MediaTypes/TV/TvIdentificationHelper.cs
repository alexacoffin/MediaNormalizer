using System.Text.RegularExpressions;
using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;

namespace Application.Normalization.MediaTypes.TV;

internal sealed class TvIdentificationHelper
{
    private static readonly Regex TrailingYearExpression = new(
        @"\s+(?:\((?<year>\d{4})\)|(?<year>\d{4}))$",
        RegexOptions.CultureInvariant);

    private readonly IFileManager fileManager;
    private readonly IImdbClient imdbClient;

    public TvIdentificationHelper(IFileManager fileManager, IImdbClient imdbClient)
    {
        this.fileManager = fileManager;
        this.imdbClient = imdbClient;
    }

    public async Task<TvIdentificationRunResult> IdentifyAsync(
        TvShowFolderGroupingResult groupingResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupingResult);

        var identifications = new List<TvShowIdentification>();

        foreach (var showFolderGroup in groupingResult.ShowFolderGroups
            .OrderBy(group => group.TvRootPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.ShowFolderPath, StringComparer.OrdinalIgnoreCase))
        {
            identifications.Add(await IdentifyShowFolderAsync(showFolderGroup, cancellationToken));
        }

        var looseFileIdentifications = await IdentifyLooseFilesAsync(
            groupingResult.UnsupportedFileGroups,
            cancellationToken);

        return new TvIdentificationRunResult(
            identifications.ToArray(),
            looseFileIdentifications);
    }

    internal string? TryReadShowMetadata(TvShowFolderGroup showFolderGroup) =>
        fileManager.TryReadTextFile(Path.Combine(showFolderGroup.ShowFolderPath, "tvshow.nfo"));

    private async Task<TvShowIdentification> IdentifyShowFolderAsync(
        TvShowFolderGroup showFolderGroup,
        CancellationToken cancellationToken)
    {
        var attempts = new List<TvShowMatchAttempt>();
        var metadata = TvShowMetadataParser.TryParse(TryReadShowMetadata(showFolderGroup));

        if (!string.IsNullOrWhiteSpace(metadata?.ImdbId))
        {
            var idAttempt = await TryMatchMetadataImdbIdAsync(metadata.ImdbId, cancellationToken);
            attempts.Add(idAttempt);

            if (StopsMatching(idAttempt))
            {
                return new TvShowIdentification(showFolderGroup, null, attempts.ToArray());
            }
        }

        if (!string.IsNullOrWhiteSpace(metadata?.Title))
        {
            var metadataTitleAttempt = await TryMatchTitleAsync(
                metadata.Title,
                metadata.Year,
                TvShowEvidenceSource.MetadataTitle,
                cancellationToken);
            attempts.Add(metadataTitleAttempt);

            if (StopsMatching(metadataTitleAttempt))
            {
                return new TvShowIdentification(showFolderGroup, null, attempts.ToArray());
            }
        }

        var folderAttempt = await TryMatchFolderNameAsync(showFolderGroup, cancellationToken);
        attempts.Add(folderAttempt);

        if (!StopsMatching(folderAttempt))
        {
            var filenameAttempt = await TryMatchFilenameAsync(
                showFolderGroup.FilePaths,
                cancellationToken);

            if (filenameAttempt is not null)
            {
                attempts.Add(filenameAttempt);
            }
        }

        return new TvShowIdentification(showFolderGroup, null, attempts.ToArray());
    }

    private async Task<TvShowIdentification[]> IdentifyLooseFilesAsync(
        IEnumerable<TvUnsupportedFileGroup> unsupportedFileGroups,
        CancellationToken cancellationToken)
    {
        var identifications = new List<TvShowIdentification>();

        foreach (var unsupportedFileGroup in unsupportedFileGroups
            .OrderBy(group => group.TvRootPath, StringComparer.OrdinalIgnoreCase))
        {
            var candidateFiles = new Dictionary<FilenameCandidateKey, List<string>>(
                FilenameCandidateKeyComparer.Instance);
            var unrecognizedFiles = new List<string>();

            foreach (var filePath in unsupportedFileGroup.FilePaths)
            {
                var filenameCandidate = TvFilenameParser.TryParse(filePath);
                if (filenameCandidate is null)
                {
                    unrecognizedFiles.Add(filePath);
                    continue;
                }

                var titleCandidate = CreateTitleCandidate(filenameCandidate.Title);
                if (string.IsNullOrWhiteSpace(titleCandidate.Title))
                {
                    unrecognizedFiles.Add(filePath);
                    continue;
                }

                var key = new FilenameCandidateKey(titleCandidate.Title, titleCandidate.Year);
                if (!candidateFiles.TryGetValue(key, out var files))
                {
                    files = [];
                    candidateFiles.Add(key, files);
                }

                files.Add(filePath);
            }

            foreach (var candidateFilesGroup in candidateFiles
                .OrderBy(group => group.Key.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key.Year))
            {
                var looseFileGroup = new TvUnsupportedFileGroup(
                    unsupportedFileGroup.TvRootPath,
                    SortAndDistinct(candidateFilesGroup.Value));
                var attempt = await TryMatchTitleAsync(
                    candidateFilesGroup.Key.Title,
                    candidateFilesGroup.Key.Year,
                    TvShowEvidenceSource.Filename,
                    cancellationToken);
                identifications.Add(new TvShowIdentification(null, looseFileGroup, [attempt]));
            }

            if (unrecognizedFiles.Count > 0)
            {
                var looseFileGroup = new TvUnsupportedFileGroup(
                    unsupportedFileGroup.TvRootPath,
                    SortAndDistinct(unrecognizedFiles));
                var attempt = new TvShowMatchAttempt(
                    TvShowEvidenceSource.Filename,
                    string.Empty,
                    null,
                    TvShowIdentificationStatus.InvalidCandidate,
                    null,
                    null);
                identifications.Add(new TvShowIdentification(null, looseFileGroup, [attempt]));
            }
        }

        return identifications.ToArray();
    }

    public TvShowFolderGroupingResult GroupByShowFolder(
        IEnumerable<TvMediaFile> mediaFiles)
    {
        ArgumentNullException.ThrowIfNull(mediaFiles);

        var showFolders = new Dictionary<ShowFolderKey, List<string>>(
            ShowFolderKeyComparer.Instance);
        var unsupportedFiles = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var mediaFile in mediaFiles)
        {
            if (!TryGetFileLocation(mediaFile, out var location))
            {
                continue;
            }

            if (location.IsDirectlyInTvRoot)
            {
                AddFile(unsupportedFiles, location.TvRootPath, location.FilePath);
                continue;
            }

            var key = new ShowFolderKey(location.TvRootPath, location.ShowFolderPath!);
            if (!showFolders.TryGetValue(key, out var files))
            {
                files = [];
                showFolders.Add(key, files);
            }

            files.Add(location.FilePath);
        }

        return new TvShowFolderGroupingResult(
            showFolders
                .Select(group => new TvShowFolderGroup(
                    group.Key.TvRootPath,
                    group.Key.ShowFolderPath,
                    Path.GetFileName(group.Key.ShowFolderPath),
                    SortAndDistinct(group.Value)))
                .OrderBy(group => group.TvRootPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.ShowFolderPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            unsupportedFiles
                .Select(group => new TvUnsupportedFileGroup(
                    group.Key,
                    SortAndDistinct(group.Value)))
                .OrderBy(group => group.TvRootPath, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool TryGetFileLocation(
        TvMediaFile? mediaFile,
        out FileLocation location)
    {
        location = null!;

        if (mediaFile is null
            || string.IsNullOrWhiteSpace(mediaFile.TvRootPath)
            || string.IsNullOrWhiteSpace(mediaFile.FilePath)
            || !TryGetFullPath(mediaFile.TvRootPath, out var tvRootPath)
            || !TryGetFullPath(mediaFile.FilePath, out var filePath))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(tvRootPath, filePath);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal)
            || IsOutsideRoot(relativePath))
        {
            return false;
        }

        var pathSegments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length == 0)
        {
            return false;
        }

        if (pathSegments.Length == 1)
        {
            location = new FileLocation(tvRootPath, filePath, null, true);
            return true;
        }

        var showFolderPath = Path.Combine(tvRootPath, pathSegments[0]);
        location = new FileLocation(tvRootPath, filePath, showFolderPath, false);
        return true;
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (PathTooLongException)
        {
        }

        fullPath = string.Empty;
        return false;
    }

    private static bool IsOutsideRoot(string relativePath) =>
        Path.IsPathRooted(relativePath)
        || string.Equals(relativePath, "..", StringComparison.Ordinal)
        || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);

    private static void AddFile(
        Dictionary<string, List<string>> filesByTvRoot,
        string tvRootPath,
        string filePath)
    {
        if (!filesByTvRoot.TryGetValue(tvRootPath, out var files))
        {
            files = [];
            filesByTvRoot.Add(tvRootPath, files);
        }

        files.Add(filePath);
    }

    private static string[] SortAndDistinct(IEnumerable<string> filePaths) =>
        filePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<TvShowMatchAttempt> TryMatchFolderNameAsync(
        TvShowFolderGroup showFolderGroup,
        CancellationToken cancellationToken)
    {
        var candidate = CreateTitleCandidate(showFolderGroup.ShowFolderName);
        return await TryMatchTitleAsync(
            candidate.Title,
            candidate.Year,
            TvShowEvidenceSource.FolderName,
            cancellationToken);
    }

    private async Task<TvShowMatchAttempt> TryMatchMetadataImdbIdAsync(
        string imdbId,
        CancellationToken cancellationToken)
    {
        var result = await imdbClient.GetByIdAsync(imdbId, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return new TvShowMatchAttempt(
                TvShowEvidenceSource.MetadataImdbId,
                imdbId,
                null,
                result.Error?.Kind == ImdbErrorKind.NotFound
                    ? TvShowIdentificationStatus.NoExactMatch
                    : TvShowIdentificationStatus.LookupFailed,
                null,
                result.Error);
        }

        var details = result.Value;
        return new TvShowMatchAttempt(
            TvShowEvidenceSource.MetadataImdbId,
            imdbId,
            TryParseYear(details.Year),
            details.Type == ImdbTitleType.Series
                ? TvShowIdentificationStatus.Matched
                : TvShowIdentificationStatus.NoExactMatch,
            details.Type == ImdbTitleType.Series
                ? new ImdbTitleSummary(details.ImdbId, details.Title, details.Year, details.Type)
                : null,
            null);
    }

    private async Task<TvShowMatchAttempt> TryMatchTitleAsync(
        string title,
        int? year,
        TvShowEvidenceSource evidenceSource,
        CancellationToken cancellationToken)
    {
        var candidate = CreateTitleCandidate(title);
        var candidateYear = year ?? candidate.Year;

        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            return new TvShowMatchAttempt(
                evidenceSource,
                candidate.Title,
                candidateYear,
                TvShowIdentificationStatus.InvalidCandidate,
                null,
                null);
        }

        var searchResult = await imdbClient.SearchAsync(
            candidate.Title,
            candidateYear,
            ImdbTitleType.Series,
            page: 1,
            cancellationToken);

        if (!searchResult.IsSuccess || searchResult.Value is null)
        {
            return new TvShowMatchAttempt(
                evidenceSource,
                candidate.Title,
                candidateYear,
                searchResult.Error?.Kind == ImdbErrorKind.NotFound
                    ? TvShowIdentificationStatus.NoExactMatch
                    : TvShowIdentificationStatus.LookupFailed,
                null,
                searchResult.Error ?? new ImdbError(
                    ImdbErrorKind.InvalidResponse,
                    "OMDb returned a successful response without search results."));
        }

        var exactMatches = searchResult.Value.Results
            .Where(result => string.Equals(
                NormalizeTitle(result.Title),
                candidate.Title,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new TvShowMatchAttempt(
            evidenceSource,
            candidate.Title,
            candidateYear,
            exactMatches.Length switch
            {
                0 => TvShowIdentificationStatus.NoExactMatch,
                1 => TvShowIdentificationStatus.Matched,
                _ => TvShowIdentificationStatus.AmbiguousExactMatches
            },
            exactMatches.Length == 1 ? exactMatches[0] : null,
            null);
    }

    private static bool StopsMatching(TvShowMatchAttempt attempt) =>
        attempt.Status is TvShowIdentificationStatus.Matched
            or TvShowIdentificationStatus.LookupFailed;

    private async Task<TvShowMatchAttempt?> TryMatchFilenameAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var candidates = filePaths
            .Select(TvFilenameParser.TryParse)
            .Where(candidate => candidate is not null)
            .Select(candidate => CreateTitleCandidate(candidate!.Title))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Title))
            .Distinct(TitleCandidateComparer.Instance)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length > 1)
        {
            return new TvShowMatchAttempt(
                TvShowEvidenceSource.Filename,
                string.Empty,
                null,
                TvShowIdentificationStatus.ConflictingFilenameCandidates,
                null,
                null);
        }

        return await TryMatchTitleAsync(
            candidates[0].Title,
            candidates[0].Year,
            TvShowEvidenceSource.Filename,
            cancellationToken);
    }

    private static int? TryParseYear(string? year) =>
        int.TryParse(year, out var parsedYear) ? parsedYear : null;

    private static TitleCandidate CreateTitleCandidate(string showFolderName)
    {
        var normalizedName = NormalizeTitle(showFolderName);
        var trailingYear = TrailingYearExpression.Match(normalizedName);

        if (!trailingYear.Success)
        {
            return new TitleCandidate(normalizedName, null);
        }

        var title = normalizedName[..trailingYear.Index].Trim();
        var year = int.Parse(trailingYear.Groups["year"].Value);
        return new TitleCandidate(title, year);
    }

    private static string NormalizeTitle(string? title) =>
        string.Join(
            ' ',
            (title ?? string.Empty)
                .Replace('.', ' ')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class ShowFolderKey
    {
        public ShowFolderKey(string tvRootPath, string showFolderPath)
        {
            TvRootPath = tvRootPath;
            ShowFolderPath = showFolderPath;
        }

        public string TvRootPath { get; }

        public string ShowFolderPath { get; }
    }

    private sealed class ShowFolderKeyComparer : IEqualityComparer<ShowFolderKey>
    {
        public static ShowFolderKeyComparer Instance { get; } = new();

        public bool Equals(ShowFolderKey? left, ShowFolderKey? right) =>
            ReferenceEquals(left, right)
            || (left is not null
                && right is not null
                && string.Equals(left.TvRootPath, right.TvRootPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.ShowFolderPath, right.ShowFolderPath, StringComparison.OrdinalIgnoreCase));

        public int GetHashCode(ShowFolderKey key) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(key.TvRootPath),
                StringComparer.OrdinalIgnoreCase.GetHashCode(key.ShowFolderPath));
    }

    private sealed class FileLocation
    {
        public FileLocation(
            string tvRootPath,
            string filePath,
            string? showFolderPath,
            bool isDirectlyInTvRoot)
        {
            TvRootPath = tvRootPath;
            FilePath = filePath;
            ShowFolderPath = showFolderPath;
            IsDirectlyInTvRoot = isDirectlyInTvRoot;
        }

        public string TvRootPath { get; }

        public string FilePath { get; }

        public string? ShowFolderPath { get; }

        public bool IsDirectlyInTvRoot { get; }
    }

    private sealed class TitleCandidate
    {
        public TitleCandidate(string title, int? year)
        {
            Title = title;
            Year = year;
        }

        public string Title { get; }

        public int? Year { get; }
    }

    private sealed class FilenameCandidateKey
    {
        public FilenameCandidateKey(string title, int? year)
        {
            Title = title;
            Year = year;
        }

        public string Title { get; }

        public int? Year { get; }
    }

    private sealed class FilenameCandidateKeyComparer : IEqualityComparer<FilenameCandidateKey>
    {
        public static FilenameCandidateKeyComparer Instance { get; } = new();

        public bool Equals(FilenameCandidateKey? left, FilenameCandidateKey? right) =>
            ReferenceEquals(left, right)
            || (left is not null
                && right is not null
                && string.Equals(left.Title, right.Title, StringComparison.OrdinalIgnoreCase)
                && left.Year == right.Year);

        public int GetHashCode(FilenameCandidateKey key) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Title), key.Year);
    }

    private sealed class TitleCandidateComparer : IEqualityComparer<TitleCandidate>
    {
        public static TitleCandidateComparer Instance { get; } = new();

        public bool Equals(TitleCandidate? left, TitleCandidate? right) =>
            ReferenceEquals(left, right)
            || (left is not null
                && right is not null
                && string.Equals(left.Title, right.Title, StringComparison.OrdinalIgnoreCase)
                && left.Year == right.Year);

        public int GetHashCode(TitleCandidate candidate) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(candidate.Title), candidate.Year);
    }
}
