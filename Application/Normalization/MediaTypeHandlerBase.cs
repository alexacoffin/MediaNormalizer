using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
using Microsoft.Extensions.Logging;

namespace Application.Normalization;

public abstract class MediaTypeHandlerBase
{
    private readonly INormalizationRunsRepository? runsRepository;
    private readonly ILogger logger;

    protected MediaTypeHandlerBase(
        INormalizationRunsRepository? runsRepository = null,
        ILogger? logger = null)
    {
        this.runsRepository = runsRepository;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public abstract Task<MediaTypeNormalizationResult> Normalize(
        CancellationToken cancellationToken = default);

    public virtual Task PersistAsync(
        MediaTypeNormalizationRequest mediaType,
        long normalizationRunId,
        MediaTypeNormalizationResult result,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public virtual Task ReconcileAsync(
        MediaTypeNormalizationResult result,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<MediaTypeNormalizationResult> ProcessAsync(
        MediaTypeNormalizationRequest mediaType,
        long? normalizationRunId,
        CancellationToken cancellationToken = default)
    {
        var result = await Normalize(cancellationToken);
        if (!normalizationRunId.HasValue)
        {
            return result;
        }

        try
        {
            await PersistAsync(mediaType, normalizationRunId.Value, result, cancellationToken);
            await ReconcileAsync(result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return result.WithPersistenceFailure(exception);
        }
    }

    public Task MarkRunFailedAsync(
        long runId,
        Exception exception,
        CancellationToken cancellationToken = default) =>
        MarkRunFailedAsync(runsRepository, logger, runId, exception, cancellationToken);

    public static async Task MarkRunFailedAsync(
        INormalizationRunsRepository? runsRepository,
        ILogger logger,
        long runId,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        logger.LogError(
            exception,
            "Database persistence failed while persisting normalization data; filesystem results are retained.");
        if (runsRepository is null)
        {
            return;
        }

        try
        {
            await runsRepository.UpsertAsync(
                new NormalizationRunUpsert(runId, DateTime.UtcNow, "Failed", exception.Message, null),
                cancellationToken);
        }
        catch (Exception statusException)
        {
            logger.LogError(
                statusException,
                "Database persistence failed while marking the normalization run failed; filesystem results are retained.");
        }
    }
}
