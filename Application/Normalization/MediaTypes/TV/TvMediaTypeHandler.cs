using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Normalization;
using Application.Normalization.MediaTypes.TV.Internals;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeHandler : IMediaTypeHandler
{
    private readonly string[] mediaLocations;
    private readonly IFileManager fileManager;
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
        identificationHelper = new TvIdentificationHelper(fileManager, imdbClient);
        formatter = new TvMediaTypeFormatter(fileManager, imdbClient, outputDirectory);
    }

    private readonly TvIdentificationHelper identificationHelper;
    private readonly TvMediaTypeFormatter formatter;

    public async Task<MediaTypeNormalizationResult> Normalize()
    {
        var formattingResult = await NormalizeAsync();
        return new MediaTypeNormalizationResult(
            formattingResult.FileResults.Select(MapResult),
            formattingResult.DeletedDirectories);
    }

    public async Task<TvMediaTypeFormattingResult> NormalizeAsync(
        CancellationToken cancellationToken = default)
    {
        var mediaFiles = mediaLocations
            .SelectMany(tvRoot => fileManager.FindMediaFiles(tvRoot)
                .Select(filePath => new TvMediaFile(tvRoot, filePath)))
            .ToArray();

        filesToNormalize = mediaFiles
            .Select(mediaFile => mediaFile.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        groupingResult = identificationHelper.GroupByShowFolder(mediaFiles);
        identificationResult = await identificationHelper.IdentifyAsync(groupingResult, cancellationToken);
        return await formatter.FormatAsync(identificationResult, cancellationToken);
    }

    private static MediaFileNormalizationResult MapResult(TvMediaFileFormattingResult result) =>
        new(
            result.SourceFilePath,
            result.DestinationFilePath,
            result.Status,
            result.Message);
}
