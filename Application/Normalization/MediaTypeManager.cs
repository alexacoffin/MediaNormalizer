using Application.Abstractions.Database;
using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Normalization.MediaTypes.Movies;
using Application.Normalization.MediaTypes.TV;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Normalization;

public sealed class MediaTypeManager(
    IFileManager fileManager,
    IImdbClient imdbClient,
    ITvNormalizationInventoryProvider? tvInventoryProvider = null,
    IMediaTitlesRepository? titlesRepository = null,
    IMediaFilesRepository? filesRepository = null,
    INormalizationFileResultsRepository? fileResultsRepository = null,
    INormalizationDeletedDirectoriesRepository? deletedDirectoriesRepository = null,
    INormalizationRunsRepository? runsRepository = null,
    ILogger<MediaTypeManager>? logger = null)
{
    public async Task<MediaTypeNormalizationResult> Process(
        MediaTypeNormalizationRequest mediaType,
        string[] locations,
        CancellationToken cancellationToken = default)
        => await Process(mediaType, locations, null, cancellationToken);

    public async Task<MediaTypeNormalizationResult> Process(
        MediaTypeNormalizationRequest mediaType,
        string[] locations,
        long? normalizationRunId,
        CancellationToken cancellationToken = default)
    {
        if (!mediaType.Enabled)
        {
            return MediaTypeNormalizationResult.Empty;
        }

        MediaTypeHandlerBase? handler = mediaType.Id switch
        {
            MediaType.Tv => new TvMediaTypeHandler(
                locations,
                mediaType.OutputDirectory,
                fileManager,
                imdbClient,
                tvInventoryProvider,
                titlesRepository,
                filesRepository,
                fileResultsRepository,
                deletedDirectoriesRepository,
                runsRepository,
                logger),
            MediaType.Movies => new MovieMediaTypeHandler(locations, runsRepository, logger),
            _ => null
        };

        if (handler is null)
        {
            return MediaTypeNormalizationResult.Empty;
        }

        return await handler.ProcessAsync(mediaType, normalizationRunId, cancellationToken);
    }

    public Task MarkRunFailedAsync(
        long runId,
        Exception exception,
        CancellationToken cancellationToken = default) =>
        MediaTypeHandlerBase.MarkRunFailedAsync(
            runsRepository,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaTypeManager>.Instance,
            runId,
            exception,
            cancellationToken);
}
