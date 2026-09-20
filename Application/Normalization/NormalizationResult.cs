namespace Application.Normalization;

public enum MediaFileNormalizationStatus
{
    Renamed,
    AlreadyNormalized,
    Skipped,
    Failed
}

public sealed class MediaFileNormalizationResult
{
    public MediaFileNormalizationResult(
        string sourceFilePath,
        string? destinationFilePath,
        MediaFileNormalizationStatus status,
        string message,
        string sourceRole = "Intake",
        string? omdbEntryId = null,
        string? titleName = null,
        short? releaseYear = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        DateOnly? airDate = null,
        string? episodeTitle = null)
    {
        SourceFilePath = sourceFilePath;
        DestinationFilePath = destinationFilePath;
        Status = status;
        Message = message;
        SourceRole = sourceRole;
        OmdbEntryId = omdbEntryId;
        TitleName = titleName;
        ReleaseYear = releaseYear;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
        AirDate = airDate;
        EpisodeTitle = episodeTitle;
    }

    public string SourceFilePath { get; }

    public string? DestinationFilePath { get; }

    public MediaFileNormalizationStatus Status { get; }

    public string Message { get; }

    public string SourceRole { get; }

    public string? OmdbEntryId { get; }

    public string? TitleName { get; }

    public short? ReleaseYear { get; }

    public int? SeasonNumber { get; }

    public int? EpisodeNumber { get; }

    public DateOnly? AirDate { get; }

    public string? EpisodeTitle { get; }
}

public sealed class MediaTypeNormalizationResult
{
    public MediaTypeNormalizationResult(
        IEnumerable<MediaFileNormalizationResult> fileResults,
        IEnumerable<string> deletedDirectories,
        IEnumerable<long>? observedFileIds = null,
        IEnumerable<long>? observedTitleIds = null,
        bool processedSuccessfully = false,
        Exception? persistenceFailure = null)
    {
        ArgumentNullException.ThrowIfNull(fileResults);
        ArgumentNullException.ThrowIfNull(deletedDirectories);

        FileResults = fileResults.ToArray();
        DeletedDirectories = deletedDirectories.ToArray();
        ObservedFileIds = (observedFileIds ?? []).Distinct().ToArray();
        ObservedTitleIds = (observedTitleIds ?? []).Distinct().ToArray();
        ProcessedSuccessfully = processedSuccessfully;
        PersistenceFailure = persistenceFailure;
    }

    public static MediaTypeNormalizationResult Empty { get; } = new([], []);

    public MediaFileNormalizationResult[] FileResults { get; }

    public string[] DeletedDirectories { get; }

    public long[] ObservedFileIds { get; }

    public long[] ObservedTitleIds { get; }

    public bool ProcessedSuccessfully { get; }

    public Exception? PersistenceFailure { get; }

    public MediaTypeNormalizationResult WithPersistenceFailure(Exception exception) =>
        new(
            FileResults,
            DeletedDirectories,
            ObservedFileIds,
            ObservedTitleIds,
            ProcessedSuccessfully,
            exception);
}

public sealed class NormalizationResult
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public NormalizationResult(
        IEnumerable<MediaFileNormalizationResult> fileResults,
        IEnumerable<string> deletedDirectories)
    {
        ArgumentNullException.ThrowIfNull(fileResults);
        ArgumentNullException.ThrowIfNull(deletedDirectories);

        var files = fileResults.ToArray();
        var deletedDirectoryPaths = deletedDirectories.ToArray();

        var renamed = files
            .Where(file => file.Status == MediaFileNormalizationStatus.Renamed)
            .ToArray();
        var skipped = files
            .Where(file => file.Status == MediaFileNormalizationStatus.Skipped)
            .ToArray();
        var failed = files
            .Where(file => file.Status == MediaFileNormalizationStatus.Failed)
            .ToArray();
        var alreadyNormalized = files
            .Where(file => file.Status == MediaFileNormalizationStatus.AlreadyNormalized)
            .ToArray();

        Renamed = CreateSummary(renamed.Select(file => file.DestinationFilePath ?? file.SourceFilePath));
        Skipped = CreateSummary(skipped.Select(file => file.SourceFilePath));
        Failed = CreateSummary(failed.Select(file => file.SourceFilePath));
        AlreadyNormalized = CreateSummary(
            alreadyNormalized.Select(file => file.DestinationFilePath ?? file.SourceFilePath));
        RemovedFromIntake = CreateSummary(renamed.Select(file => file.SourceFilePath));
        DeletedDirectories = CreateSummary(deletedDirectoryPaths);
    }

    public static NormalizationResult Empty { get; } = new([], []);

    public string Status => "completed";

    public NormalizationSummary Renamed { get; }

    public NormalizationSummary Skipped { get; }

    public NormalizationSummary Failed { get; }

    public NormalizationSummary AlreadyNormalized { get; }

    public NormalizationSummary RemovedFromIntake { get; }

    public NormalizationSummary DeletedDirectories { get; }

    private static NormalizationSummary CreateSummary(IEnumerable<string> paths)
    {
        var orderedPaths = paths
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer)
            .ToArray();
        return new NormalizationSummary(orderedPaths.Length, orderedPaths);
    }
}

public sealed class NormalizationSummary
{
    public NormalizationSummary(int count, string[] paths)
    {
        Count = count;
        Paths = paths;
    }

    public int Count { get; }

    public string[] Paths { get; }
}
