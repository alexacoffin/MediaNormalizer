using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
using Application.Normalization;
using Microsoft.Extensions.Logging;

namespace Business.Services;

public sealed class NormalizationService : INormalizationService
{
    private readonly MediaLibraryNormalizationRequest mediaLibrary;
    private readonly MediaTypeManager mediaTypeManager;
    private readonly INormalizationRunsRepository? runsRepository;
    private readonly ILogger<NormalizationService> logger;

    public NormalizationService(
        MediaLibraryNormalizationRequest mediaLibrary,
        MediaTypeManager mediaTypeManager,
        INormalizationRunsRepository? runsRepository = null,
        ILogger<NormalizationService>? logger = null)
    {
        this.mediaLibrary = mediaLibrary;
        this.mediaTypeManager = mediaTypeManager;
        this.runsRepository = runsRepository;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NormalizationService>.Instance;
    }

    public async Task<NormalizationResult> NormalizeMediaFiles(CancellationToken cancellationToken = default)
    {
        var fileResults = new List<MediaFileNormalizationResult>();
        var deletedDirectories = new List<string>();
        long? normalizationRunId = null;
        try
        {
            normalizationRunId = await TryStartRunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogPersistenceFailure("starting", exception);
        }

        foreach (var mediaType in mediaLibrary.MediaTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MediaTypeNormalizationResult result;
            try
            {
                result = await mediaTypeManager.Process(
                    mediaType,
                    mediaLibrary.Locations,
                    normalizationRunId,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (normalizationRunId.HasValue)
                {
                    await mediaTypeManager.MarkRunFailedAsync(
                        normalizationRunId.Value,
                        new OperationCanceledException("Normalization was canceled."),
                        CancellationToken.None);
                }

                throw;
            }
            catch (Exception exception)
            {
                if (normalizationRunId.HasValue)
                {
                    await mediaTypeManager.MarkRunFailedAsync(
                        normalizationRunId.Value,
                        exception,
                        CancellationToken.None);
                }

                throw;
            }

            fileResults.AddRange(result.FileResults);
            deletedDirectories.AddRange(result.DeletedDirectories);

            if (normalizationRunId.HasValue && result.PersistenceFailure is not null)
            {
                await mediaTypeManager.MarkRunFailedAsync(
                    normalizationRunId.Value,
                    result.PersistenceFailure,
                    cancellationToken);
                normalizationRunId = null;
            }
        }

        var normalizationResult = new NormalizationResult(fileResults, deletedDirectories);
        if (normalizationRunId.HasValue)
        {
            try
            {
                await runsRepository!.UpsertAsync(
                    new NormalizationRunUpsert(normalizationRunId, DateTime.UtcNow, "Completed", null, null),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await mediaTypeManager.MarkRunFailedAsync(
                    normalizationRunId.Value,
                    exception,
                    cancellationToken);
            }
        }

        return normalizationResult;
    }

    private async Task<long?> TryStartRunAsync(CancellationToken cancellationToken)
    {
        if (!PersistenceAvailable)
        {
            return null;
        }

        var run = await runsRepository!.UpsertAsync(
            new NormalizationRunUpsert(null, null, "Running", null, DateTime.UtcNow),
            cancellationToken);
        return run.Id;
    }

    private bool PersistenceAvailable => runsRepository is not null;

    private void LogPersistenceFailure(string operation, Exception exception) =>
        logger.LogError(exception, "Database persistence failed while {Operation}; filesystem results are retained.", operation);
}
