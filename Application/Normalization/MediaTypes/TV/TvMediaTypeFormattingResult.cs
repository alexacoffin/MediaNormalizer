using Application.Normalization;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeFormattingResult
{
    public TvMediaTypeFormattingResult(
        TvMediaFileFormattingResult[] fileResults,
        string[]? deletedDirectories = null)
    {
        FileResults = fileResults;
        DeletedDirectories = deletedDirectories ?? [];
    }

    public TvMediaFileFormattingResult[] FileResults { get; }

    public string[] DeletedDirectories { get; }

    public int RenamedCount => FileResults.Count(result => result.Status == MediaFileNormalizationStatus.Renamed);

    public int AlreadyNormalizedCount => FileResults.Count(result => result.Status == MediaFileNormalizationStatus.AlreadyNormalized);

    public int SkippedCount => FileResults.Count(result => result.Status == MediaFileNormalizationStatus.Skipped);

    public int FailedCount => FileResults.Count(result => result.Status == MediaFileNormalizationStatus.Failed);
}
