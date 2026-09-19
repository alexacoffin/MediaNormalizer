using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvShowMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_NormalizesFolderNameAndMatchesOneExactSeries()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                2022,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [Series("tt14452776", "Bob's Burgers", "2022")],
                1,
                1)));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("Bob's_Burgers_(2022)")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.FinalAttempt.EvidenceSource);
        Assert.Single(identification.Attempts);
        Assert.Equal("Bob's Burgers", identification.CandidateTitle);
        Assert.Equal(2022, identification.CandidateYear);
        Assert.Equal("tt14452776", identification.MatchedSeries?.ImdbId);
        imdbClient.Verify(client => client.SearchAsync(
            "Bob's Burgers",
            2022,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentifyAsync_RequiresAnExactNormalizedSeriesTitle()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [Series("tt0000001", "Bear", "2022")],
                1,
                1)));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("Bob's.Burgers")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Status);
        Assert.Null(identification.MatchedSeries);
    }

    [Fact]
    public async Task IdentifyAsync_MarksMultipleExactMatchesAsAmbiguous()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [
                    Series("tt0000001", "Bob's Burgers", "2022"),
                    Series("tt0000002", "Bob's.Burgers", "2023")
                ],
                2,
                1)));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("Bob's Burgers")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.AmbiguousExactMatches, identification.Status);
        Assert.Null(identification.MatchedSeries);
    }

    [Fact]
    public async Task IdentifyAsync_MarksTypedOmdbFailuresAsLookupFailuresAndContinues()
    {
        var searches = new List<(string Title, int? Year, ImdbTitleType? Type, int Page)>();
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int?, ImdbTitleType?, int, CancellationToken>(
                (title, year, type, page, _) => searches.Add((title, year, type, page)))
            .Returns((string title, int? year, ImdbTitleType? type, int page, CancellationToken cancellationToken) =>
                Task.FromResult(title == "Bob's Burgers"
                    ? ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                        ImdbErrorKind.RateLimit,
                        "Request limit reached."))
                    : ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                        [Series("tt0000003", "Severance", "2022")],
                        1,
                        1))));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var identifications = (await helper.IdentifyAsync(GroupingResult(
            ShowFolder("Bob's Burgers"), ShowFolder("Severance")))).ShowIdentifications;

        Assert.Equal(2, identifications.Length);
        Assert.Contains(identifications, identification =>
            identification.CandidateTitle == "Severance"
            && identification.Status == TvShowIdentificationStatus.Matched);
        var failedIdentification = Assert.Single(
            identifications,
            identification => identification.CandidateTitle == "Bob's Burgers");
        Assert.Equal(TvShowIdentificationStatus.LookupFailed, failedIdentification.Status);
        Assert.Equal(ImdbErrorKind.RateLimit, failedIdentification.LookupError?.Kind);
        Assert.Equal(2, searches.Count);
        Assert.Contains(("Bob's Burgers", null, ImdbTitleType.Series, 1), searches);
        Assert.Contains(("Severance", null, ImdbTitleType.Series, 1), searches);
    }

    [Fact]
    public async Task IdentifyAsync_MarksSuccessfulSearchWithoutResultsAsLookupFailure()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(null!));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var identification = Assert.Single((await helper.IdentifyAsync(
            GroupingResult(ShowFolder("Bob's Burgers")))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.LookupFailed, identification.Status);
        Assert.Equal(ImdbErrorKind.InvalidResponse, identification.LookupError?.Kind);
        Assert.Null(identification.MatchedSeries);
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
}
