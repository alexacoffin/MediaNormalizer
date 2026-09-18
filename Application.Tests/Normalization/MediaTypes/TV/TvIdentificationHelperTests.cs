using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Tests.TestDoubles;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvIdentificationHelperTests
{
    private readonly TvIdentificationHelper helper = new(
        new StubFileManager(),
        new StubImdbClient());

    [Fact]
    public void GroupByShowFolder_GroupsNestedFilesUnderOneShow()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var result = helper.GroupByShowFolder(
        [
            MediaFile(tvRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv"),
            MediaFile(tvRoot, "The Bear", "Season 02", "The.Bear.S02E01.mkv"),
            MediaFile(tvRoot, "The Bear", "Extras", "Behind.The.Scenes.mkv")
        ]);

        var group = Assert.Single(result.ShowFolderGroups);
        Assert.Equal(Path.Combine(tvRoot, "The Bear"), group.ShowFolderPath);
        Assert.Equal("The Bear", group.ShowFolderName);
        Assert.Equal(3, group.FilePaths.Length);
        Assert.Empty(result.UnsupportedFileGroups);
    }

    [Fact]
    public void GroupByShowFolder_KeepsShowFoldersSeparate()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var result = helper.GroupByShowFolder(
        [
            MediaFile(tvRoot, "Severance", "Season 01", "Severance.S01E01.mkv"),
            MediaFile(tvRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv")
        ]);

        Assert.Equal(
            ["Severance", "The Bear"],
            result.ShowFolderGroups.Select(group => group.ShowFolderName));
    }

    [Fact]
    public void GroupByShowFolder_KeepsMatchingShowNamesFromSeparateRootsSeparate()
    {
        var firstRoot = Path.Combine(Path.GetTempPath(), "LibraryOne", "TV");
        var secondRoot = Path.Combine(Path.GetTempPath(), "LibraryTwo", "TV");
        var result = helper.GroupByShowFolder(
        [
            MediaFile(firstRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv"),
            MediaFile(secondRoot, "The Bear", "Season 02", "The.Bear.S02E01.mkv")
        ]);

        Assert.Equal(2, result.ShowFolderGroups.Length);
        Assert.All(result.ShowFolderGroups, group => Assert.Equal("The Bear", group.ShowFolderName));
        Assert.False(string.Equals(
            result.ShowFolderGroups[0].TvRootPath,
            result.ShowFolderGroups[1].TvRootPath,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GroupByShowFolder_RemovesDuplicatePathsAndSortsFiles()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var laterEpisode = MediaFile(tvRoot, "The Bear", "Season 01", "The.Bear.S01E02.mkv");
        var earlierEpisode = MediaFile(tvRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv");
        var result = helper.GroupByShowFolder([laterEpisode, earlierEpisode, laterEpisode]);

        var group = Assert.Single(result.ShowFolderGroups);
        Assert.Equal(
            [earlierEpisode.FilePath, laterEpisode.FilePath],
            group.FilePaths);
    }

    [Fact]
    public void GroupByShowFolder_RecordsFilesDirectlyInTvRootAsUnsupported()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var directFile = new TvMediaFile(tvRoot, Path.Combine(tvRoot, "Unsorted.S01E01.mkv"));
        var nestedFile = MediaFile(tvRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv");
        var result = helper.GroupByShowFolder([directFile, nestedFile]);

        Assert.Single(result.ShowFolderGroups);
        var unsupportedGroup = Assert.Single(result.UnsupportedFileGroups);
        Assert.Equal(tvRoot, unsupportedGroup.TvRootPath);
        Assert.Equal([directFile.FilePath], unsupportedGroup.FilePaths);
    }

    [Fact]
    public void GroupByShowFolder_IgnoresFilesOutsideTheirDeclaredTvRoot()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var externalFile = new TvMediaFile(
            tvRoot,
            Path.Combine(Path.GetTempPath(), "Elsewhere", "The.Bear.S01E01.mkv"));

        var result = helper.GroupByShowFolder([externalFile]);

        Assert.Empty(result.ShowFolderGroups);
        Assert.Empty(result.UnsupportedFileGroups);
    }

    private static TvMediaFile MediaFile(string tvRoot, params string[] relativeSegments) =>
        new(tvRoot, Path.Combine([tvRoot, .. relativeSegments]));

    [Fact]
    public void TryReadShowMetadata_ReadsTvShowNfoFromShowFolder()
    {
        var expectedPath = Path.Combine("C:\\TV", "The Bear", "tvshow.nfo");
        var helper = new TvIdentificationHelper(
            new StubFileManager { TextFileReader = path => path == expectedPath ? "<tvshow />" : null },
            new StubImdbClient());
        var showFolderGroup = new TvShowFolderGroup(
            "C:\\TV",
            Path.Combine("C:\\TV", "The Bear"),
            "The Bear",
            []);

        var metadata = helper.TryReadShowMetadata(showFolderGroup);

        Assert.Equal("<tvshow />", metadata);
    }

    private sealed class StubImdbClient : IImdbClient
    {
        public Task<ImdbResult<ImdbSearchPage>> SearchAsync(
            string title,
            int? year = null,
            ImdbTitleType? type = null,
            int page = 1,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
}
