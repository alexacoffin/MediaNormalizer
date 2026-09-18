namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvFilenameCandidate
{
    public TvFilenameCandidate(
        string title,
        int? seasonNumber,
        int? episodeNumber,
        DateOnly? airDate,
        bool isMultiEpisode)
    {
        Title = title;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
        AirDate = airDate;
        IsMultiEpisode = isMultiEpisode;
    }

    public string Title { get; }

    public int? SeasonNumber { get; }

    public int? EpisodeNumber { get; }

    public DateOnly? AirDate { get; }

    public bool IsMultiEpisode { get; }
}
