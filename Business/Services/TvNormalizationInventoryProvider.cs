using Application.Abstractions.Database;
using Application.Normalization;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Business.Services;

public sealed class TvNormalizationInventoryProvider(
    IMediaTitlesRepository? titlesRepository,
    IMediaFilesRepository? filesRepository,
    ILogger<TvNormalizationInventoryProvider>? logger = null) : ITvNormalizationInventoryProvider
{
    private readonly ILogger<TvNormalizationInventoryProvider> logger =
        logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TvNormalizationInventoryProvider>.Instance;

    public async Task<MediaTypeNormalizationInventory?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (titlesRepository is null || filesRepository is null)
        {
            return null;
        }

        try
        {
            var titles = await titlesRepository.GetActiveByMediaTypeAsync(
                (int)MediaType.Tv,
                cancellationToken);
            var files = await filesRepository.GetActiveByMediaTypeAsync(
                (int)MediaType.Tv,
                cancellationToken);
            return new MediaTypeNormalizationInventory(files, titles);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Database persistence failed while loading the TV destination inventory; normalization will use the full scan.");
            return null;
        }
    }
}
