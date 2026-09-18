namespace Application.Abstractions.Imdb.Models;

public sealed class ImdbTitleSummary
{
    public ImdbTitleSummary(string imdbId, string title, string year, ImdbTitleType type)
    {
        ImdbId = imdbId;
        Title = title;
        Year = year;
        Type = type;
    }

    public string ImdbId { get; }

    public string Title { get; }

    public string Year { get; }

    public ImdbTitleType Type { get; }
}
