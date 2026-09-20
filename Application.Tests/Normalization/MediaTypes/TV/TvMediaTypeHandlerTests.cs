using Application.Normalization;
using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeHandlerTests
{
    [Fact]
    public async Task Normalize_DiscoversAndIdentifiesEachShowFolder()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var firstEpisode = Path.Combine(libraryRoot, "Bob's Burgers", "Season 01", "Bob's.Burgers.S01E01.mkv");
        var secondEpisode = Path.Combine(libraryRoot, "Bob's Burgers", "Season 02", "Bob's.Burgers.S02E01.mkv");
        var scannedDirectories = new List<string>();
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(It.IsAny<string>()))
            .Callback<string>(directory => scannedDirectories.Add(directory))
            .Returns((string directory) => directory == libraryRoot
                ? [firstEpisode, secondEpisode]
                : []);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                null,
                Application.Abstractions.Imdb.Models.ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "Bob's Burgers", "2022", ImdbTitleType.Series)],
                1,
                1)));
        imdbClient
            .Setup(client => client.GetEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(
                new ImdbError(ImdbErrorKind.NotFound, "Not found.")));
        var handler = new TvMediaTypeHandler([libraryRoot], outputRoot, fileManager.Object, imdbClient.Object);

        var formattingResult = await handler.NormalizeAsync();

        Assert.Equal([libraryRoot, outputRoot], scannedDirectories);
        Assert.Equal(2, formattingResult.RenamedCount);
        Assert.All(
            formattingResult.FileResults,
            result => Assert.StartsWith(outputRoot, result.DestinationFilePath!, StringComparison.OrdinalIgnoreCase));
        imdbClient.Verify(client => client.SearchAsync(
            "Bob's Burgers",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Normalize_ScansAllConfiguredRootsWhenTheyAreEmpty()
    {
        var firstRoot = Path.Combine(Path.GetTempPath(), "LibraryOne");
        var secondRoot = Path.Combine(Path.GetTempPath(), "LibraryTwo");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var scannedDirectories = new List<string>();
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(It.IsAny<string>()))
            .Callback<string>(directory => scannedDirectories.Add(directory))
            .Returns([]);
        var handler = new TvMediaTypeHandler(
            [firstRoot, secondRoot],
            outputRoot,
            fileManager.Object,
            new Mock<IImdbClient>().Object);

        var result = await handler.NormalizeAsync();

        Assert.Equal([firstRoot, secondRoot, outputRoot], scannedDirectories);
        Assert.Empty(result.FileResults);
        fileManager.Verify(manager => manager.FindMediaFiles(firstRoot), Times.Once);
        fileManager.Verify(manager => manager.FindMediaFiles(secondRoot), Times.Once);
        fileManager.Verify(manager => manager.FindMediaFiles(outputRoot), Times.Once);
    }

    [Fact]
    public async Task Normalize_ProcessesDestinationFilesWhenIntakeIsEmpty()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(outputRoot, "Unsorted.S01E01.mkv");
        var scannedDirectories = new List<string>();
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(It.IsAny<string>()))
            .Callback<string>(directory => scannedDirectories.Add(directory))
            .Returns((string directory) => directory == outputRoot
                ? [destinationFile]
                : []);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object);

        var result = await handler.NormalizeAsync();

        Assert.Equal([intakeRoot, outputRoot], scannedDirectories);
        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(destinationFile, fileResult.SourceFilePath);
        Assert.Equal(MediaFileNormalizationStatus.Skipped, fileResult.Status);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Normalize_ReportsCanonicalDestinationFilesWithoutMovingOrCleaning()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(
            outputRoot,
            "Bob's Burgers",
            "Season 01",
            "Bob's Burgers - S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(intakeRoot))
            .Returns([]);
        fileManager
            .Setup(manager => manager.FindMediaFiles(outputRoot))
            .Returns([destinationFile]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Bob's Burgers",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "Bob's Burgers", string.Empty, ImdbTitleType.Series)],
                1,
                1)));
        imdbClient
            .Setup(client => client.GetEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(
                new ImdbError(ImdbErrorKind.NotFound, "Not found.")));
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object);

        var result = await handler.NormalizeAsync();

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(destinationFile, fileResult.SourceFilePath);
        Assert.Equal(destinationFile, fileResult.DestinationFilePath);
        Assert.Equal(MediaFileNormalizationStatus.AlreadyNormalized, fileResult.Status);
        fileManager.Verify(manager => manager.MoveFile(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        fileManager.Verify(manager => manager.TryDeleteEmptyDirectory(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Normalize_IgnoresMissingDestinationDirectory()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "MissingFormattedTV");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(intakeRoot))
            .Returns([]);
        fileManager
            .Setup(manager => manager.FindMediaFiles(outputRoot))
            .Throws<DirectoryNotFoundException>();
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            new Mock<IImdbClient>().Object);

        var result = await handler.NormalizeAsync();

        Assert.Empty(result.FileResults);
        fileManager.Verify(manager => manager.FindMediaFiles(intakeRoot), Times.Once);
        fileManager.Verify(manager => manager.FindMediaFiles(outputRoot), Times.Once);
    }

    [Fact]
    public async Task Normalize_DeduplicatesRootsAndDiscoveredFiles()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(intakeRoot, "FormattedTV");
        var mediaFile = Path.Combine(intakeRoot, "Unsorted.S01E01.mkv");
        var scannedDirectories = new List<string>();
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(It.IsAny<string>()))
            .Callback<string>(directory => scannedDirectories.Add(directory))
            .Returns((string directory) => directory == intakeRoot || directory == outputRoot
                ? [mediaFile]
                : []);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var handler = new TvMediaTypeHandler(
            [intakeRoot, intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object);

        var result = await handler.NormalizeAsync();

        Assert.Equal([intakeRoot, outputRoot], scannedDirectories);
        Assert.Single(result.FileResults);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Normalize_SkipsUnmatchedDirectFilesWithoutMovingThem()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var sourcePath = Path.Combine(libraryRoot, "Unsorted.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(libraryRoot))
            .Returns([sourcePath]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var handler = new TvMediaTypeHandler(
            [libraryRoot],
            Path.Combine(Path.GetTempPath(), "FormattedTV"),
            fileManager.Object,
            imdbClient.Object);

        var result = await handler.NormalizeAsync();

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Skipped, fileResult.Status);
        fileManager.Verify(manager => manager.MoveFile(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        fileManager.Verify(manager => manager.EnsureDirectory(It.IsAny<string>()), Times.Never);
        fileManager.Verify(manager => manager.TryDeleteEmptyDirectory(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Normalize_SkipsAmbiguousDirectFilesWithoutMovingThem()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var sourcePath = Path.Combine(libraryRoot, "Bob's.Burgers.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(libraryRoot))
            .Returns([sourcePath]);
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
                    new ImdbTitleSummary("tt0000001", "Bob's Burgers", "2011", ImdbTitleType.Series),
                    new ImdbTitleSummary("tt0000002", "Bob's Burgers", "2012", ImdbTitleType.Series)
                ],
                2,
                1)));
        var handler = new TvMediaTypeHandler(
            [libraryRoot],
            Path.Combine(Path.GetTempPath(), "FormattedTV"),
            fileManager.Object,
            imdbClient.Object);

        var result = await handler.NormalizeAsync();

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Skipped, fileResult.Status);
        fileManager.Verify(manager => manager.MoveFile(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        fileManager.Verify(manager => manager.EnsureDirectory(It.IsAny<string>()), Times.Never);
        fileManager.Verify(manager => manager.TryDeleteEmptyDirectory(It.IsAny<string>()), Times.Never);
    }
}
