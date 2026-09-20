using Application.Normalization;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaFileFormattingResult
{
    public TvMediaFileFormattingResult(
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
