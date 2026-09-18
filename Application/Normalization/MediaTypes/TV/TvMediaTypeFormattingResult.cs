namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeFormattingResult
{
    public TvMediaTypeFormattingResult(TvMediaFileFormattingResult[] fileResults) =>
        FileResults = fileResults;

    public TvMediaFileFormattingResult[] FileResults { get; }

    public int RenamedCount => FileResults.Count(result => result.Status == TvMediaTypeFormattingStatus.Renamed);

    public int AlreadyNormalizedCount => FileResults.Count(result => result.Status == TvMediaTypeFormattingStatus.AlreadyNormalized);

    public int SkippedCount => FileResults.Count(result => result.Status == TvMediaTypeFormattingStatus.Skipped);

    public int FailedCount => FileResults.Count(result => result.Status == TvMediaTypeFormattingStatus.Failed);
}
