namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvShowMetadata
{
    public TvShowMetadata(string? imdbId, string? title, int? year)
    {
        ImdbId = imdbId;
        Title = title;
        Year = year;
    }

    public string? ImdbId { get; }

    public string? Title { get; }

    public int? Year { get; }
}
