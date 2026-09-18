using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Application.Tests.TestDoubles;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvFilenameMatchingTests
{
    [Fact]
    public async Task IdentifyAsync_FallsFromFolderNoMatchToAConsensusFilenameMatch()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchHandler = request => request.Title == "Wrong Folder"
                ? EmptySearch()
                : SeriesSearch("The Bear")
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);
        var group = ShowFolder(
            "Wrong Folder",
            "The.Bear.S01E01.1080p.WEB-DL.mkv",
            "The.Bear.S01E02.1080p.WEB-DL.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(2, identification.Attempts.Length);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.Attempts[0].EvidenceSource);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.Filename, identification.FinalAttempt.EvidenceSource);
        Assert.Equal("The Bear", identification.FinalAttempt.CandidateTitle);
    }

    [Fact]
    public async Task IdentifyAsync_FallsFromAnOmdbNotFoundToAConsensusFilenameMatch()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchHandler = request => request.Title == "Wrong Folder"
                ? ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                    ImdbErrorKind.NotFound,
                    "Series not found."))
                : SeriesSearch("The Bear")
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);
        var group = ShowFolder("Wrong Folder", "The.Bear.S01E01.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status);
        Assert.Equal(TvShowIdentificationStatus.NoExactMatch, identification.Attempts[0].Status);
        Assert.Equal(TvShowEvidenceSource.Filename, identification.FinalAttempt.EvidenceSource);
    }

    [Fact]
    public async Task IdentifyAsync_DoesNotFallThroughAfterATerminalOmdbFailure()
    {
        var imdbClient = new FakeImdbClient
        {
            SearchHandler = _ => ImdbResult<ImdbSearchPage>.Failure(new ImdbError(
                ImdbErrorKind.RateLimit,
                "Request limit reached."))
        };
        var helper = new TvIdentificationHelper(new StubFileManager(), imdbClient);
        var group = ShowFolder("Wrong Folder", "The.Bear.S01E01.mkv");

        var identification = Assert.Single((await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([group], []))).ShowIdentifications);

        Assert.Equal(TvShowIdentificationStatus.LookupFailed, identification.Status);
        Assert.Single(identification.Attempts);
        Assert.Equal(TvShowEvidenceSource.FolderName, identification.FinalAttempt.EvidenceSource);
    }

    [Fact]
    public async Task IdentifyAsync_LeavesAShowFolderUnresolvedWhenFilenameCandidatesConflict()
    {
        var helper = new TvIdentificationHelper(new StubFileManager(), new FakeImdbClient
        {
            SearchHandler = _ => EmptySearch()
        });
        var group = ShowFolder(
            "Wrong Folder",
            "The.Bear.S01E01.mkv",
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
                Path.Combine(tvRoot, "The.Bear.S01E01.mkv"),
                Path.Combine(tvRoot, "The.Bear.S01E02.mkv"),
                Path.Combine(tvRoot, "Severance.S01E01.mkv")
            ]);
        var helper = new TvIdentificationHelper(new StubFileManager(), new FakeImdbClient
        {
            SearchHandler = request => SeriesSearch(request.Title)
        });

        var result = await helper.IdentifyAsync(
            new TvShowFolderGroupingResult([], [looseFiles]));

        Assert.Empty(result.ShowIdentifications);
        Assert.Equal(2, result.LooseFileIdentifications.Length);
        Assert.All(result.LooseFileIdentifications, identification =>
            Assert.Equal(TvShowIdentificationStatus.Matched, identification.Status));
        Assert.Contains(result.LooseFileIdentifications, identification =>
            identification.FinalAttempt.CandidateTitle == "The Bear"
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

    private sealed class FakeImdbClient : IImdbClient
    {
        public Func<SearchRequest, ImdbResult<ImdbSearchPage>> SearchHandler { get; init; } =
            _ => EmptySearch();

        public Task<ImdbResult<ImdbSearchPage>> SearchAsync(
            string title,
            int? year = null,
            ImdbTitleType? type = null,
            int page = 1,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SearchHandler(new SearchRequest(title, year, type, page)));

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
