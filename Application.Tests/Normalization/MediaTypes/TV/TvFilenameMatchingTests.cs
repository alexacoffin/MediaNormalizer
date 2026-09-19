using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvFilenameMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_FallsFromFolderNoMatchToAConsensusFilenameMatch()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((string title, int? year, ImdbTitleType? type, int page, CancellationToken cancellationToken) =>
                Task.FromResult(title == "Wrong Folder"
                    ? EmptySearch()
                    : SeriesSearch("Bob's Burgers")));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);
        var group = ShowFolder(
            "Wrong Folder",
            "Bob's.Burgers.S01E01.1080p.WEB-DL.mkv",
            "Bob's.Burgers.S01E02.1080p.WEB-DL.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(2, identification.Attempts.Length);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.Attempts[0].EvidenceSource);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.Filename, identification.FinalAttempt.EvidenceSource);
        Assert.Equal("Bob's Burgers", identification.FinalAttempt.CandidateTitle);
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromAnOmdbNotFoundToAConsensusFilenameMatch()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((string title, int? year, ImdbTitleType? type, int page, CancellationToken cancellationToken) =>
                Task.FromResult(title == "Wrong Folder"
                    ? ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                        ImdbErrorKind.NotFound,
                        "Series not found."))
                    : SeriesSearch("Bob's Burgers")));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);
        var group = ShowFolder("Wrong Folder", "Bob's.Burgers.S01E01.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.Filename, identification.FinalAttempt.EvidenceSource);
    }

    [Fact]
    public async Task IdentifyAsync_DoesNotFallThroughAfterATerminalOmdbFailure()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                ImdbErrorKind.RateLimit,
                "Request limit reached.")));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);
        var group = ShowFolder("Wrong Folder", "Bob's.Burgers.S01E01.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.LookupFailed, identification.Status);
        Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.FinalAttempt.EvidenceSource);
    }

    [Fact]
    public async Task IdentifyAsync_LeavesAShowFolderUnresolvedWhenFilenameCandidatesConflict()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptySearch());
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);
        var group = ShowFolder(
            "Wrong Folder",
            "Bob's.Burgers.S01E01.mkv",
            "Severance.S01E01.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.ConflictingFilenameCandidates, identification.Status);
        Assert.Equal(TvShowEvidenceSource.Filename, identification.FinalAttempt.EvidenceSource);
    }

    [Fact]
    public async Task IdentifyAsync_ClustersLooseFilesByFilenameCandidate()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var looseFiles = new TvUnsupportedFileGroup(
            tvRoot,
            [
                Path.Combine(tvRoot, "Bob's.Burgers.S01E01.mkv"),
                Path.Combine(tvRoot, "Bob's.Burgers.S01E02.mkv"),
                Path.Combine(tvRoot, "Severance.S01E01.mkv")
            ]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<ImdbTitleType?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((string title, int? year, ImdbTitleType? type, int page, CancellationToken cancellationToken) =>
                Task.FromResult(SeriesSearch(title)));
        var helper = new TvIdentificationHelper(new Mock<IFileManager>().Object, imdbClient.Object);

        var result = await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([], [looseFiles]));

        Assert.Empty(result.ShowIdentifications);
        Assert.Equal(2, result.LooseFileIdentifications.Length);
        Assert.All(result.LooseFileIdentifications, identification =>
            Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status));
        Assert.Contains(result.LooseFileIdentifications, identification =>
            identification.FinalAttempt.CandidateTitle == "Bob's Burgers"
            && identification.LooseFileGroup?.FilePaths.Length == 2);
        Assert.Contains(result.LooseFileIdentifications, identification =>
            identification.FinalAttempt.CandidateTitle == "Severance"
            && identification.LooseFileGroup?.FilePaths.Length == 1);
    }

    private static TvShowFolderGroup ShowFolder(string folderName, params string[] fileNames)
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var showFolder = Path.Combine(tvRoot, folderName);
        return new TvShowFolderGroup(
            tvRoot,
            showFolder,
            folderName,
            fileNames.Select(fileName => Path.Combine(showFolder, "Season 01", fileName)).ToArray());
    }

    private static ImdbResult<ImdbSearchPage> EmptySearch() =>
        ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1));

    private static ImdbResult<ImdbSearchPage> SeriesSearch(string title) =>
        ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
            [new ImdbTitleSummary($"tt{title.Length:D7}", title, "2022", ImdbTitleType.Series)],
            1,
            1));
}
