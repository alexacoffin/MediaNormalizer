namespace Application.Abstractions.Imdb.Models;

public sealed class ImdbTitleDetails
{
    public ImdbTitleDetails(
        string imdbId,
        string title,
        string year,
        ImdbTitleType type,
        string? seriesImdbId,
        int? seasonNumber,
        int? episodeNumber)
    {
        ImdbId = imdbId;
        Title = title;
        Year = year;
        Type = type;
        SeriesImdbId = seriesImdbId;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
    }

    public string ImdbId { get; }

    public string Title { get; }

    public string Year { get; }

    public ImdbTitleType Type { get; }

    public string? SeriesImdbId { get; }

    public int? SeasonNumber { get; }

    public int? EpisodeNumber { get; }
}
