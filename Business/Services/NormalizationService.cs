using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
using Application.Normalization;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Business.Services;

public sealed class NormalizationService : INormalizationService
{
    private readonly MediaLibraryNormalizationRequest mediaLibrary;
    private readonly MediaTypeHandler mediaTypeHandler;
    private readonly INormalizationRunsRepository? runsRepository;
    private readonly IMediaTitlesRepository? titlesRepository;
    private readonly IMediaFilesRepository? filesRepository;
    private readonly INormalizationFileResultsRepository? fileResultsRepository;
    private readonly INormalizationDeletedDirectoriesRepository? deletedDirectoriesRepository;
    private readonly ILogger<NormalizationService> logger;

    public NormalizationService(
        MediaLibraryNormalizationRequest mediaLibrary,
        MediaTypeHandler mediaTypeHandler,
        INormalizationRunsRepository? runsRepository = null,
        IMediaTitlesRepository? titlesRepository = null,
        IMediaFilesRepository? filesRepository = null,
        INormalizationFileResultsRepository? fileResultsRepository = null,
        INormalizationDeletedDirectoriesRepository? deletedDirectoriesRepository = null,
        ILogger<NormalizationService>? logger = null)
    {
        this.mediaLibrary = mediaLibrary;
        this.mediaTypeHandler = mediaTypeHandler;
        this.runsRepository = runsRepository;
        this.titlesRepository = titlesRepository;
        this.filesRepository = filesRepository;
        this.fileResultsRepository = fileResultsRepository;
        this.deletedDirectoriesRepository = deletedDirectoriesRepository;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NormalizationService>.Instance;
    }

    public async Task<NormalizationResult> NormalizeMediaFiles(CancellationToken cancellationToken = default)
    {
        var fileResults = new List<MediaFileNormalizationResult>();
        var deletedDirectories = new List<string>();
        long? normalizationRunId = null;
        var titleIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var seenTitleIds = new HashSet<long>();
        var seenFileIds = new HashSet<long>();
        var tvProcessedSuccessfully = false;

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
                result = await mediaTypeHandler.Process(mediaType, mediaLibrary.Locations, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (normalizationRunId.HasValue)
                {
                    await MarkRunFailedAsync(normalizationRunId.Value, new OperationCanceledException("Normalization was canceled."), CancellationToken.None);
                }

                throw;
            }
            catch (Exception exception)
            {
                if (normalizationRunId.HasValue)
                {
                    await MarkRunFailedAsync(normalizationRunId.Value, exception, CancellationToken.None);
                }

                throw;
            }

            fileResults.AddRange(result.FileResults);
            deletedDirectories.AddRange(result.DeletedDirectories);

            if (normalizationRunId.HasValue && mediaType.Id == MediaType.Tv)
            {
                tvProcessedSuccessfully |= mediaType.Enabled;
                try
                {
                    await PersistTvResultAsync(normalizationRunId.Value, mediaType, result, titleIds, seenTitleIds, seenFileIds, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await MarkRunFailedAsync(normalizationRunId.Value, exception, cancellationToken);
                    normalizationRunId = null;
                }
            }
        }

        var normalizationResult = new NormalizationResult(fileResults, deletedDirectories);
        if (normalizationRunId.HasValue)
        {
            try
            {
                if (tvProcessedSuccessfully)
                {
                    await ReconcileTvRowsAsync(seenTitleIds, seenFileIds, cancellationToken);
                }
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
                await MarkRunFailedAsync(normalizationRunId.Value, exception, cancellationToken);
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

    private async Task PersistTvResultAsync(
        long runId,
        MediaTypeNormalizationRequest mediaType,
        MediaTypeNormalizationResult result,
        IDictionary<string, long> titleIds,
        ISet<long> seenTitleIds,
        ISet<long> seenFileIds,
        CancellationToken cancellationToken)
    {
        foreach (var fileResult in result.FileResults)
        {
            long? titleId = null;
            if (!string.IsNullOrWhiteSpace(fileResult.OmdbEntryId))
            {
                if (!titleIds.TryGetValue(fileResult.OmdbEntryId, out var persistedTitleId))
                {
                    var title = await titlesRepository!.UpsertAsync(
                        new MediaTitleUpsert(null, (int)mediaType.Id, fileResult.OmdbEntryId, fileResult.TitleName, fileResult.ReleaseYear, runId, true),
                        cancellationToken);
                    persistedTitleId = title.Id;
                    titleIds[fileResult.OmdbEntryId] = persistedTitleId;
                }

                titleId = persistedTitleId;
                seenTitleIds.Add(persistedTitleId);
            }

            var currentPath = fileResult.Status is MediaFileNormalizationStatus.Renamed or MediaFileNormalizationStatus.AlreadyNormalized
                ? fileResult.DestinationFilePath ?? fileResult.SourceFilePath
                : fileResult.SourceFilePath;
            var existingFile = await filesRepository!.GetByCurrentPathAsync(currentPath, true, cancellationToken);
            var persistedFile = await filesRepository.UpsertAsync(
                new MediaFileUpsert(
                    existingFile?.Id,
                    titleId,
                    (int)mediaType.Id,
                    currentPath,
                    fileResult.DestinationFilePath,
                    fileResult.SeasonNumber,
                    fileResult.EpisodeNumber,
                    fileResult.AirDate,
                    fileResult.EpisodeTitle,
                    fileResult.Status.ToString(),
                    fileResult.Message,
                    existingFile?.FirstSeenRunId ?? runId,
                    runId,
                    true),
                cancellationToken);
            seenFileIds.Add(persistedFile.Id);

            await fileResultsRepository!.UpsertAsync(
                new NormalizationFileResultUpsert(null, runId, persistedFile.Id, titleId, (int)mediaType.Id, fileResult.SourceFilePath, fileResult.DestinationFilePath, fileResult.Status.ToString(), fileResult.Message, fileResult.SourceRole),
                cancellationToken);
        }

        foreach (var directory in result.DeletedDirectories)
        {
            await deletedDirectoriesRepository!.UpsertAsync(
                new NormalizationDeletedDirectoryUpsert(null, runId, (int)mediaType.Id, directory),
                cancellationToken);
        }
    }

    private async Task ReconcileTvRowsAsync(
        ISet<long> seenTitleIds,
        ISet<long> seenFileIds,
        CancellationToken cancellationToken)
    {
        if (!PersistenceAvailable)
        {
            return;
        }

        var tvTypeId = (int)MediaType.Tv;
        foreach (var file in await filesRepository!.GetActiveByMediaTypeAsync(tvTypeId, cancellationToken))
        {
            if (!seenFileIds.Contains(file.Id))
            {
                await filesRepository.DeleteAsync(file.Id, cancellationToken);
            }
        }

        foreach (var title in await titlesRepository!.GetActiveByMediaTypeAsync(tvTypeId, cancellationToken))
        {
            if (!seenTitleIds.Contains(title.Id))
            {
                await titlesRepository.DeleteAsync(title.Id, cancellationToken);
            }
        }
    }

    private async Task MarkRunFailedAsync(long runId, Exception exception, CancellationToken cancellationToken)
    {
        LogPersistenceFailure("persisting normalization data", exception);
        try
        {
            await runsRepository!.UpsertAsync(
                new NormalizationRunUpsert(runId, DateTime.UtcNow, "Failed", exception.Message, null),
                cancellationToken);
        }
        catch (Exception statusException)
        {
            LogPersistenceFailure("marking the normalization run failed", statusException);
        }
    }

    private bool PersistenceAvailable =>
        runsRepository is not null && titlesRepository is not null && filesRepository is not null
        && fileResultsRepository is not null && deletedDirectoriesRepository is not null;

    private void LogPersistenceFailure(string operation, Exception exception) =>
        logger.LogError(exception, "Database persistence failed while {Operation}; filesystem results are retained.", operation);
}
