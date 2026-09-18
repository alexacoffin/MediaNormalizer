namespace Application.Abstractions.Imdb.Models;

public sealed class ImdbSearchPage
{
    public ImdbSearchPage(IReadOnlyList<ImdbTitleSummary> results, int totalResults, int page)
    {
        Results = results;
        TotalResults = totalResults;
        Page = page;
    }

    public IReadOnlyList<ImdbTitleSummary> Results { get; }

    public int TotalResults { get; }

    public int Page { get; }
}
