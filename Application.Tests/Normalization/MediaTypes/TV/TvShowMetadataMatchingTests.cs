using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvShowMetadataMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_MatchesASeriesUsingTheNfoImdbId()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.GetByIdAsync("tt14452776", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Success(new ImdbTitleDetails(
                "tt14452776",
                "Bob's Burgers",
                "2022",
                ImdbTitleType.Series,
                null,
                null,
                null)));
        var helper = CreateHelper(
            "<tvshow><uniqueid type=\"imdb\">tt14452776</uniqueid></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        var attempt = Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.MetadataImdbId, attempt.EvidenceSource);
        Assert.Equal("tt14452776", attempt.MatchedSeries?.ImdbId);
        imdbClient.Verify(client => client.GetByIdAsync("tt14452776", It.IsAny<CancellationToken>()), Times.Once);
        imdbClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromANotFoundNfoIdToNfoTitleAndPremieredYear()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.GetByIdAsync("tt0000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(
                ImdbErrorKind.NotFound,
                "Movie not found!")));
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                2022,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "Bob's Burgers", "2022", ImdbTitleType.Series)],
                1,
                1)));
        var helper = CreateHelper(
            "<tvshow><imdbid>tt0000000</imdbid><title>Bob's Burgers</title><premiered>2022-06-23</premiered></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(2, identification.Attempts.Length);
        Assert.Equal(TvShowEvidenceSource.MetadataImdbId, identification.Attempts[0].EvidenceSource);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.MetadataTitle, identification.FinalAttempt.EvidenceSource);
        imdbClient.Verify(client => client.GetByIdAsync("tt0000000", It.IsAny<CancellationToken>()), Times.Once);
        imdbClient.Verify(client => client.SearchAsync(
            "Bob's Burgers",
            2022,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromMalformedNfoToFolderTitle()
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
                [new ImdbTitleSummary("tt14452776", "Bob's Burgers", "2022", ImdbTitleType.Series)],
                1,
                1)));
        var helper = CreateHelper("<tvshow><title>Bob's Burgers</title>", imdbClient);

        var identification = await IdentifyAsync(helper, "Bob's Burgers");

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        var attempt = Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.FolderName, attempt.EvidenceSource);
        imdbClient.Verify(client => client.SearchAsync(
            "Bob's Burgers",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentifyAsync_MarksMetadataIdAsNoExactMatchWhenResultIsNotSeries()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.GetByIdAsync("tt0000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Success(new ImdbTitleDetails(
                "tt0000001",
                "Bob's Burgers Movie",
                "2022",
                ImdbTitleType.Movie,
                null,
                null,
                null)));
        imdbClient
            .Setup(client => client.SearchAsync(
                "Wrong Folder Name",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var helper = CreateHelper(
            "<tvshow><imdbid>tt0000001</imdbid></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Status);
        Assert.Equal(2, identification.Attempts.Length);
        Assert.Equal(TvShowEvidenceSource.MetadataImdbId, identification.Attempts[0].EvidenceSource);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.FinalAttempt.EvidenceSource);
        Assert.Null(identification.MatchedSeries);
        imdbClient.Verify(client => client.GetByIdAsync("tt0000001", It.IsAny<CancellationToken>()), Times.Once);
        imdbClient.Verify(client => client.SearchAsync(
            "Wrong Folder Name",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IdentifyAsync_StopsOnMetadataIdLookupFailure()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.GetByIdAsync("tt0000002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(
                ImdbErrorKind.RateLimit,
                "Request limit reached.")));
        var helper = CreateHelper(
            "<tvshow><imdbid>tt0000002</imdbid><title>Bob's Burgers</title></tvshow>",
            imdbClient);

        var identification = await IdentifyAsync(helper, "Wrong Folder Name");

        Assert.Equal(TvShowIdentificationStatus.LookupFailed, identification.Status);
        Assert.Equal(ImdbErrorKind.RateLimit, identification.LookupError?.Kind);
        Assert.Single(identification.Attempts);
        imdbClient.Verify(client => client.GetByIdAsync("tt0000002", It.IsAny<CancellationToken>()), Times.Once);
        imdbClient.VerifyNoOtherCalls();
    }

    private static TvIdentificationHelper CreateHelper(
        string nfoContent,
        Mock<IImdbClient> imdbClient)
    {
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.TryReadTextFile(It.IsAny<string>()))
            .Returns(nfoContent);
        return new TvIdentificationHelper(fileManager.Object, imdbClient.Object);
    }

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
}
