using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvIdentificationHelperTests
{
    private readonly TvIdentificationHelper helper = new(
        new Mock<IFileManager>().Object,
        new Mock<IImdbClient>().Object);

    [Fact]
    public void GroupByShowFolder_GroupsNestedFilesUnderOneShow()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var result = helper.GroupByShowFolder(
        [
            MediaFile(tvRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv"),
            MediaFile(tvRoot, "Bob's Burgers", "Season 02", "Bob's.Burgers.S02E01.mkv"),
            MediaFile(tvRoot, "Bob's Burgers", "Extras", "Behind.The.Scenes.mkv")
        ]);

        var group = Assert.Single(result.ShowFolderGroups);
        Assert.Equal(Path.Combine(tvRoot, "Bob's Burgers"), group.ShowFolderPath);
        Assert.Equal("Bob's Burgers", group.ShowFolderName);
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
            MediaFile(tvRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv")
        ]);

        Assert.Equal(
            ["Bob's Burgers", "Severance"],
            result.ShowFolderGroups.Select(group => group.ShowFolderName));
    }

    [Fact]
    public void GroupByShowFolder_KeepsMatchingShowNamesFromSeparateRootsSeparate()
    {
        var firstRoot = Path.Combine(Path.GetTempPath(), "LibraryOne", "TV");
        var secondRoot = Path.Combine(Path.GetTempPath(), "LibraryTwo", "TV");
        var result = helper.GroupByShowFolder(
        [
            MediaFile(firstRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv"),
            MediaFile(secondRoot, "Bob's Burgers", "Season 02", "Bob's.Burgers.S02E01.mkv")
        ]);

        Assert.Equal(2, result.ShowFolderGroups.Length);
        Assert.All(result.ShowFolderGroups, group => Assert.Equal("Bob's Burgers", group.ShowFolderName));
        Assert.False(string.Equals(
            result.ShowFolderGroups[0].TvRootPath,
            result.ShowFolderGroups[1].TvRootPath,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GroupByShowFolder_RemovesDuplicatePathsAndSortsFiles()
    {
        var tvRoot = Path.Combine(Path.GetTempPath(), "TV");
        var laterEpisode = MediaFile(tvRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E02.mkv");
        var earlierEpisode = MediaFile(tvRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv");
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
        var nestedFile = MediaFile(tvRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv");
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
            Path.Combine(Path.GetTempPath(), "Elsewhere", "Bob's.Burgers.S01E01.mkv"));

        var result = helper.GroupByShowFolder([externalFile]);

        Assert.Empty(result.ShowFolderGroups);
        Assert.Empty(result.UnsupportedFileGroups);
    }

    private static TvMediaFile MediaFile(string tvRoot, params string[] relativeSegments) =>
        new(tvRoot, Path.Combine([tvRoot, .. relativeSegments]));

    [Fact]
    public void TryReadShowMetadata_ReadsTvShowNfoFromShowFolder()
    {
        var expectedPath = Path.Combine("C:\\TV", "Bob's Burgers", "tvshow.nfo");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.TryReadTextFile(expectedPath))
            .Returns("<tvshow />");
        var helper = new TvIdentificationHelper(
            fileManager.Object,
            new Mock<IImdbClient>().Object);
        var showFolderGroup = new TvShowFolderGroup(
            "C:\\TV",
            Path.Combine("C:\\TV", "Bob's Burgers"),
            "Bob's Burgers",
            []);

        var metadata = helper.TryReadShowMetadata(showFolderGroup);

        Assert.Equal("<tvshow />", metadata);
        fileManager.Verify(manager => manager.TryReadTextFile(expectedPath), Times.Once);
    }
}
