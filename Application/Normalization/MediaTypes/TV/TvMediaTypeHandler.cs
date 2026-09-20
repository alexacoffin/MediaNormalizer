using Application.Abstractions.Database;
using Application.Abstractions.FileSystem;
using Application.Abstractions.Database.Models;
using Application.Abstractions.Imdb;
using Application.Normalization;
using Application.Normalization.MediaTypes.TV.Internals;
using Microsoft.Extensions.Logging;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeHandler : MediaTypeHandlerBase
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string[] mediaLocations;
    private readonly IFileManager fileManager;
    private readonly string outputDirectory;
    private readonly ITvNormalizationInventoryProvider? inventoryProvider;
    private readonly IMediaTitlesRepository? titlesRepository;
    private readonly IMediaFilesRepository? filesRepository;
    private readonly INormalizationFileResultsRepository? fileResultsRepository;
    private readonly INormalizationDeletedDirectoriesRepository? deletedDirectoriesRepository;
    private MediaTypeNormalizationInventory? inventory;
    private bool inventoryLoaded;
    private string[] filesToNormalize = [];
    private TvShowFolderGroupingResult groupingResult = new([], []);
    private TvIdentificationRunResult identificationResult = new([], []);
    private InventoryResult[] inventoryResults = [];
    private readonly HashSet<long> persistedFileIds = [];
    private readonly HashSet<long> persistedTitleIds = [];

    private bool PersistenceAvailable =>
        titlesRepository is not null
        && filesRepository is not null
        && fileResultsRepository is not null
        && deletedDirectoriesRepository is not null;

    public TvMediaTypeHandler(
        string[] locations,
        string outputDirectory,
        IFileManager fileManager,
        IImdbClient imdbClient,
        ITvNormalizationInventoryProvider? inventoryProvider = null,
        IMediaTitlesRepository? titlesRepository = null,
        IMediaFilesRepository? filesRepository = null,
        INormalizationFileResultsRepository? fileResultsRepository = null,
        INormalizationDeletedDirectoriesRepository? deletedDirectoriesRepository = null,
        INormalizationRunsRepository? runsRepository = null,
        ILogger? logger = null)
        : base(runsRepository, logger)
    {
        mediaLocations = locations;
        this.fileManager = fileManager;
        this.outputDirectory = Path.GetFullPath(outputDirectory);
        this.inventoryProvider = inventoryProvider;
        this.titlesRepository = titlesRepository;
        this.filesRepository = filesRepository;
        this.fileResultsRepository = fileResultsRepository;
        this.deletedDirectoriesRepository = deletedDirectoriesRepository;
        identificationHelper = new TvIdentificationHelper(fileManager, imdbClient);
        formatter = new TvMediaTypeFormatter(fileManager, imdbClient, outputDirectory);
    }

    private readonly TvIdentificationHelper identificationHelper;
    private readonly TvMediaTypeFormatter formatter;

    public override async Task<MediaTypeNormalizationResult> Normalize(CancellationToken cancellationToken = default)
    {
        var formattingResult = await NormalizeAsync(cancellationToken);
        return new MediaTypeNormalizationResult(
            formattingResult.FileResults.Select(MapResult),
            formattingResult.DeletedDirectories,
            inventoryResults.Select(result => result.FileId),
            inventoryResults
                .Where(result => result.TitleId.HasValue)
                .Select(result => result.TitleId!.Value),
            processedSuccessfully: true);
    }

    public override async Task PersistAsync(
        MediaTypeNormalizationRequest mediaType,
        long normalizationRunId,
        MediaTypeNormalizationResult result,
        CancellationToken cancellationToken = default)
    {
        if (!PersistenceAvailable)
        {
            return;
        }

        persistedFileIds.Clear();
        persistedTitleIds.Clear();
        var titleIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileResult in result.FileResults)
        {
            long? titleId = null;
            if (!string.IsNullOrWhiteSpace(fileResult.OmdbEntryId))
            {
                if (!titleIds.TryGetValue(fileResult.OmdbEntryId, out var persistedTitleId))
                {
                    var title = await titlesRepository!.UpsertAsync(
                        new MediaTitleUpsert(
                            null,
                            (int)mediaType.Id,
                            fileResult.OmdbEntryId,
                            fileResult.TitleName,
                            fileResult.ReleaseYear,
                            normalizationRunId,
                            true),
                        cancellationToken);
                    persistedTitleId = title.Id;
                    titleIds[fileResult.OmdbEntryId] = persistedTitleId;
                }

                titleId = persistedTitleId;
                persistedTitleIds.Add(persistedTitleId);
            }

            var currentPath = fileResult.Status is MediaFileNormalizationStatus.Renamed or MediaFileNormalizationStatus.AlreadyNormalized
                ? fileResult.DestinationFilePath ?? fileResult.SourceFilePath
                : fileResult.SourceFilePath;
            var existingFile = await filesRepository!.GetByCurrentPathAsync(
                currentPath,
                true,
                cancellationToken);
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
                    existingFile?.FirstSeenRunId ?? normalizationRunId,
                    normalizationRunId,
                    true),
                cancellationToken);
            persistedFileIds.Add(persistedFile.Id);

            await fileResultsRepository!.UpsertAsync(
                new NormalizationFileResultUpsert(
                    null,
                    normalizationRunId,
                    persistedFile.Id,
                    titleId,
                    (int)mediaType.Id,
                    fileResult.SourceFilePath,
                    fileResult.DestinationFilePath,
                    fileResult.Status.ToString(),
                    fileResult.Message,
                    fileResult.SourceRole),
                cancellationToken);
        }

        foreach (var directory in result.DeletedDirectories)
        {
            await deletedDirectoriesRepository!.UpsertAsync(
                new NormalizationDeletedDirectoryUpsert(
                    null,
                    normalizationRunId,
                    (int)mediaType.Id,
                    directory),
                cancellationToken);
        }
    }

    public override async Task ReconcileAsync(
        MediaTypeNormalizationResult result,
        CancellationToken cancellationToken = default)
    {
        if (!PersistenceAvailable)
        {
            return;
        }

        var seenFileIds = result.ObservedFileIds
            .Concat(persistedFileIds)
            .ToHashSet();
        var seenTitleIds = result.ObservedTitleIds
            .Concat(persistedTitleIds)
            .ToHashSet();
        const int tvTypeId = (int)Domain.Enums.MediaType.Tv;

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

    public async Task<TvMediaTypeFormattingResult> NormalizeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!inventoryLoaded)
        {
            inventory = inventoryProvider is null
                ? null
                : await inventoryProvider.GetAsync(cancellationToken);
            inventoryLoaded = true;
        }

        var mediaFilesByPath = new Dictionary<string, TvMediaFile>(PathComparer);
        foreach (var scanLocation in GetScanLocations())
        {
            foreach (var filePath in FindMediaFiles(scanLocation))
            {
                var normalizedFilePath = Path.GetFullPath(filePath);
                mediaFilesByPath.TryAdd(
                    normalizedFilePath,
                    new TvMediaFile(scanLocation.RootPath, normalizedFilePath, scanLocation.IsDestinationScan));
            }
        }

        var mediaFiles = mediaFilesByPath.Values.ToArray();
        inventoryResults = mediaFiles
            .Where(mediaFile => mediaFile.IsFromDestinationScan)
            .Select(TryCreateInventoryResult)
            .Where(result => result is not null)
            .Select(result => result!)
            .ToArray();
        var filesToProcess = mediaFiles
            .Where(mediaFile => !mediaFile.IsFromDestinationScan || !inventoryResults.Any(result => PathsEqual(result.CurrentPath, mediaFile.FilePath)))
            .ToArray();

        filesToNormalize = filesToProcess
            .Select(mediaFile => mediaFile.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        groupingResult = identificationHelper.GroupByShowFolder(filesToProcess);
        identificationResult = await identificationHelper.IdentifyAsync(groupingResult, cancellationToken);
        var formattingResult = await formatter.FormatAsync(identificationResult, cancellationToken);
        return new TvMediaTypeFormattingResult(
            formattingResult.FileResults
                .Concat(inventoryResults.Select(result => result.FormattingResult))
                .OrderBy(result => result.SourceFilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            formattingResult.DeletedDirectories);
    }

    private IEnumerable<ScanLocation> GetScanLocations()
    {
        var intakeLocations = mediaLocations
            .Select(rootPath => new ScanLocation(Path.GetFullPath(rootPath), false, false));

        return intakeLocations
            .Append(new ScanLocation(outputDirectory, true, true))
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

    private readonly record struct ScanLocation(
        string RootPath,
        bool IsOptional,
        bool IsDestinationScan);

    private InventoryResult? TryCreateInventoryResult(TvMediaFile mediaFile)
    {
        if (inventory is null)
        {
            return null;
        }

        var file = inventory.Files.FirstOrDefault(candidate =>
            PathsEqual(candidate.CurrentPath, mediaFile.FilePath)
            && !string.IsNullOrWhiteSpace(candidate.CanonicalPath)
            && PathsEqual(candidate.CanonicalPath!, mediaFile.FilePath)
            && candidate.IsActive
            && (string.Equals(candidate.LastStatus, nameof(MediaFileNormalizationStatus.Renamed), StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.LastStatus, nameof(MediaFileNormalizationStatus.AlreadyNormalized), StringComparison.OrdinalIgnoreCase)));
        if (file is null)
        {
            return null;
        }

        if (!file.TitleId.HasValue)
        {
            return null;
        }

        var title = inventory.Titles.FirstOrDefault(candidate =>
            candidate.Id == file.TitleId.Value && candidate.IsActive);
        if (title is null)
        {
            return null;
        }

        var formattingResult = new TvMediaFileFormattingResult(
            mediaFile.FilePath,
            file.CanonicalPath,
            MediaFileNormalizationStatus.AlreadyNormalized,
            "Destination file is already normalized.",
            "Destination",
            title?.OmdbEntryId,
            title?.Name,
            title?.ReleaseYear,
            file.SeasonNumber,
            file.EpisodeNumber,
            file.AirDate,
            file.EpisodeTitle);
        return new InventoryResult(file.Id, file.TitleId, file.CurrentPath, formattingResult);
    }

    private static bool PathsEqual(string left, string right) =>
        TryGetFullPath(left, out var leftPath)
        && TryGetFullPath(right, out var rightPath)
        && string.Equals(
            leftPath,
            rightPath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            return false;
        }
        catch (NotSupportedException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private sealed record InventoryResult(
        long FileId,
        long? TitleId,
        string CurrentPath,
        TvMediaFileFormattingResult FormattingResult);

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
