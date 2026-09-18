using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Application.Tests.TestDoubles;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvShowMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_NormalizesFolderNameAndMatchesOneExactSeries()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [Series("tt14452776", "The Bear", "2022")], 1, 1))
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("The_Bear_(2022)")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.FinalAttempt.EvidenceSource);
        Assert.Single(identification.Attempts);
        Assert.Equal("The Bear", identification.CandidateTitle);
        Assert.Equal(2022, identification.CandidateYear);
        Assert.Equal("tt14452776", identification.MatchedSeries?.ImdbId);
        var search = Assert.Single(imdbClient.Searches);
        Assert.Equal("The Bear", search.Title);
        Assert.Equal(2022, search.Year);
        Assert.Equal(ImdbTitleType.Series, search.Type);
        Assert.Equal(1, search.Page);
    }

    [Fact]
    public async Task IdentifyAsync_RequiresAnExactNormalizedSeriesTitle()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [Series("tt0000001", "Bear", "2022")], 1, 1))
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("The.Bear")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Status);
        Assert.Null(identification.MatchedSeries);
    }

    [Fact]
    public async Task IdentifyAsync_MarksMultipleExactMatchesAsAmbiguous()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [
                    Series("tt0000001", "The Bear", "2022"),
                    Series("tt0000002", "The.Bear", "2023")
                ],
                2,
                1))
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("The Bear")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.AmbiguousExactMatches, identification.Status);
        Assert.Null(identification.MatchedSeries);
    }

    [Fact]
    public async Task IdentifyAsync_MarksTypedOmdbFailuresAsLookupFailuresAndContinues()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchHandler = request => request.Title == "The Bear"
                ? ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                    ImdbErrorKind.RateLimit,
                    "Request limit reached."))
                : ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                    [Series("tt0000003", "Severance", "2022")], 1, 1))
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);

        var identifications = (await helper.IdentifyAsync(GroupingResult(
            ShowFolder("The Bear"), ShowFolder("Severance")))).ShowIdentifications;

        Assert.Equal(2, identifications.Length);
        Assert.Equal(TvShowIdentificationStatus.Matched, identifications[0].Status);
        Assert.Equal(TvShowIdentificationStatus.LookupFailed, identifications[1].Status);
        Assert.Equal(ImdbErrorKind.RateLimit, identifications[1].LookupError?.Kind);
        Assert.Equal(2, imdbClient.Searches.Count);
    }

    private static TvShowFolderGroup ShowFolder(string name)
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var showFolder = Path.Combine(tvRoot, name);
        return new TvShowFolderGroup(
            tvRoot,
            showFolder,
            name,
            [Path.Combine(showFolder, "Season 01", "Episode.mkv")]);
    }

    private static TvShowFolderGroupingResult GroupingResult(
        params TvShowFolderGroup[] showFolderGroups) =>
        new(showFolderGroups, []);

    private static ImdbTitleSummary Series(string imdbId, string title, string year) =>
        new(imdbId, title, year, ImdbTitleType.Series);

    private sealed class FakeImdbClient : IImdbClient
    {
        public ImdbResult<ImdbSearchPage> SearchResponse { get; init; } =
            ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1));

        public Func<SearchRequest, ImdbResult<ImdbSearchPage>>? SearchHandler { get; init; }

        public List<SearchRequest> Searches { get; } = [];

        public Task<ImdbResult<ImdbSearchPage>> SearchAsync(
            string title,
            int? year = null,
            ImdbTitleType? type = null,
            int page = 1,
            CancellationToken cancellationToken = default)
        {
            var request = new SearchRequest(title, year, type, page);
            Searches.Add(request);
            return Task.FromResult(SearchHandler?.Invoke(request) ?? SearchResponse);
        }

        public Task<ImdbResult<ImdbTitleDetails>> GetByIdAsync(
            string imdbId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ImdbResult<ImdbTitleDetails>> GetEpisodeAsync(
            string seriesImdbId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ImdbResult<ImdbTitleDetails>.Failure(
                new ImdbError(ImdbErrorKind.NotFound, "Not found.")));
    }

    private sealed class SearchRequest
    {
        public SearchRequest(string title, int? year, ImdbTitleType? type, int page)
        {
            Title = title;
            Year = year;
            Type = type;
            Page = page;
        }

        public string Title { get; }

        public int? Year { get; }

        public ImdbTitleType? Type { get; }

        public int Page { get; }
    }
}
