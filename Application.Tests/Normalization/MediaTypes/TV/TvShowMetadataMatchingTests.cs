using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Application.Tests.TestDoubles;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvShowMetadataMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_MatchesASeriesUsingTheNfoImdbId()
    {
        var imdbClient = new FakeImdbClient
        {
            DetailsResponse = ImdbResult<ImdbTitleDetails>.Success(new ImdbTitleDetails(
                "tt14452776",
                "The Bear",
                "2022",
                ImdbTitleType.Series,
                null,
                null,
                null))
        };
        var helper = CreateHelper(
            "<tvshow><uniqueid type=\"imdb\">tt14452776</uniqueid></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        var attempt = Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.MetadataImdbId, attempt.EvidenceSource);
        Assert.Equal("tt14452776", attempt.MatchedSeries?.ImdbId);
        Assert.Equal(["tt14452776"], imdbClient.DetailRequests);
        Assert.Empty(imdbClient.SearchRequests);
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromANotFoundNfoIdToNfoTitleAndPremieredYear()
    {
        var imdbClient = new FakeImdbClient
        {
            DetailsResponse = ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(
                ImdbErrorKind.NotFound,
                "Movie not found!")),
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "The Bear", "2022", ImdbTitleType.Series)],
                1,
                1))
        };
        var helper = CreateHelper(
            "<tvshow><imdbid>tt0000000</imdbid><title>The Bear</title><premiered>2022-06-23</premiered></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(2, identification.Attempts.Length);
        Assert.Equal(TvShowEvidenceSource.MetadataImdbId, identification.Attempts[0].EvidenceSource);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.MetadataTitle, identification.FinalAttempt.EvidenceSource);
        var search = Assert.Single(imdbClient.SearchRequests);
        Assert.Equal("The Bear", search.Title);
        Assert.Equal(2022, search.Year);
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromMalformedNfoToFolderTitle()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "The Bear", "2022", ImdbTitleType.Series)],
                1,
                1))
        };
        var helper = CreateHelper("<tvshow><title>The Bear</title>", imdbClient);

        var identification = await IdentifyAsync(helper, "The Bear");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        var attempt = Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.FolderName, attempt.EvidenceSource);
        Assert.Equal("The Bear", Assert.Single(imdbClient.SearchRequests).Title);
    }

    private static TvIdentificationHelper CreateHelper(
        string nfoContent,
        FakeImdbClient imdbClient) =>
        new(
            new StubFileManager { TextFileReader = _ => nfoContent },
            imdbClient);

    private static async Task<TvShowIdentification> IdentifyAsync(
        TvIdentificationHelper helper,
        string folderName)
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var showFolder = Path.Combine(tvRoot, folderName);
        var group = new TvShowFolderGroup(
            tvRoot,
            showFolder,
            folderName,
            [Path.Combine(showFolder, "Season 01", "Episode.mkv")]);
        var result = await helper.IdentifyAsync(new TvShowFolderGroupingResult([group], []));

        return Assert.Single(result.ShowIdentifications);
    }

    private sealed class FakeImdbClient : IImdbClient
    {
        public ImdbResult<ImdbTitleDetails> DetailsResponse { get; init; } =
            ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(ImdbErrorKind.NotFound, "Not found."));

        public ImdbResult<ImdbSearchPage> SearchResponse { get; init; } =
            ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1));

        public List<string> DetailRequests { get; } = [];

        public List<SearchRequest> SearchRequests { get; } = [];

        public Task<ImdbResult<ImdbSearchPage>> SearchAsync(
            string title,
            int? year = null,
            ImdbTitleType? type = null,
            int page = 1,
            CancellationToken cancellationToken = default)
        {
            SearchRequests.Add(new SearchRequest(title, year, type, page));
            return Task.FromResult(SearchResponse);
        }

        public Task<ImdbResult<ImdbTitleDetails>> GetByIdAsync(
            string imdbId,
            CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(imdbId);
            return Task.FromResult(DetailsResponse);
        }

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
