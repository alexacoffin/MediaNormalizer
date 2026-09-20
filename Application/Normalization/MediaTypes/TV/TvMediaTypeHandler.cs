using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Normalization;
using Application.Normalization.MediaTypes.TV.Internals;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeHandler : IMediaTypeHandler
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string[] mediaLocations;
    private readonly IFileManager fileManager;
    private readonly string outputDirectory;
    private string[] filesToNormalize = [];
    private TvShowFolderGroupingResult groupingResult = new([], []);
    private TvIdentificationRunResult identificationResult = new([], []);

    public TvMediaTypeHandler(
        string[] locations,
        string outputDirectory,
        IFileManager fileManager,
        IImdbClient imdbClient)
    {
        mediaLocations = locations;
        this.fileManager = fileManager;
        this.outputDirectory = Path.GetFullPath(outputDirectory);
        identificationHelper = new TvIdentificationHelper(fileManager, imdbClient);
        formatter = new TvMediaTypeFormatter(fileManager, imdbClient, outputDirectory);
    }

    private readonly TvIdentificationHelper identificationHelper;
    private readonly TvMediaTypeFormatter formatter;

    public async Task<MediaTypeNormalizationResult> Normalize(CancellationToken cancellationToken = default)
    {
        var formattingResult = await NormalizeAsync(cancellationToken);
        return new MediaTypeNormalizationResult(
            formattingResult.FileResults.Select(MapResult),
            formattingResult.DeletedDirectories);
    }

    public async Task<TvMediaTypeFormattingResult> NormalizeAsync(
        CancellationToken cancellationToken = default)
    {
        var mediaFilesByPath = new Dictionary<string, TvMediaFile>(PathComparer);
        foreach (var scanLocation in GetScanLocations())
        {
            foreach (var filePath in FindMediaFiles(scanLocation))
            {
                var normalizedFilePath = Path.GetFullPath(filePath);
                mediaFilesByPath.TryAdd(
                    normalizedFilePath,
                    new TvMediaFile(scanLocation.RootPath, normalizedFilePath));
            }
        }

        var mediaFiles = mediaFilesByPath.Values.ToArray();

        filesToNormalize = mediaFiles
            .Select(mediaFile => mediaFile.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        groupingResult = identificationHelper.GroupByShowFolder(mediaFiles);
        identificationResult = await identificationHelper.IdentifyAsync(groupingResult, cancellationToken);
        return await formatter.FormatAsync(identificationResult, cancellationToken);
    }

    private IEnumerable<ScanLocation> GetScanLocations()
    {
        var intakeLocations = mediaLocations
            .Select(rootPath => new ScanLocation(Path.GetFullPath(rootPath), false));

        return intakeLocations
            .Append(new ScanLocation(outputDirectory, true))
            .GroupBy(location => location.RootPath, PathComparer)
            .Select(group => group.First());
    }

    private string[] FindMediaFiles(ScanLocation scanLocation)
    {
        try
        {
            return fileManager.FindMediaFiles(scanLocation.RootPath);
        }
        catch (DirectoryNotFoundException) when (scanLocation.IsOptional)
        {
            return [];
        }
    }

    private readonly record struct ScanLocation(string RootPath, bool IsOptional);

    private static MediaFileNormalizationResult MapResult(TvMediaFileFormattingResult result) =>
        new(
            result.SourceFilePath,
            result.DestinationFilePath,
            result.Status,
            result.Message,
            result.SourceRole,
            result.OmdbEntryId,
            result.TitleName,
            result.ReleaseYear,
            result.SeasonNumber,
            result.EpisodeNumber,
            result.AirDate,
            result.EpisodeTitle);
}
