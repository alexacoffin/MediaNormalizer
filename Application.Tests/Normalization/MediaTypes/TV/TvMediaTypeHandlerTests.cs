using Application.Normalization;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
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
    public async Task Normalize_SkipsCanonicalDestinationFilesFoundInDatabaseInventory()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(
            outputRoot,
            "Bob's Burgers",
            "Season 01",
            "Bob's Burgers - S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(intakeRoot)).Returns([]);
        fileManager.Setup(manager => manager.FindMediaFiles(outputRoot)).Returns([destinationFile]);
        var imdbClient = new Mock<IImdbClient>();
        var inventory = new MediaTypeNormalizationInventory(
            [new MediaFile(
                17,
                23,
                1,
                destinationFile,
                destinationFile,
                1,
                1,
                null,
                "Pilot",
                "AlreadyNormalized",
                null,
                4,
                4,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true)],
            [new MediaTitle(
                23,
                1,
                "tt14452776",
                "Bob's Burgers",
                2022,
                DateTime.UtcNow,
                DateTime.UtcNow,
                4,
                true)]);
        var inventoryProvider = CreateInventoryProvider(inventory);
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object,
            inventoryProvider.Object);

        var result = await handler.Normalize();

        Assert.IsAssignableFrom<MediaTypeHandlerBase>(handler);
        var fileResult = Assert.Single(result.FileResults);
        Assert.True(result.ProcessedSuccessfully);
        Assert.Equal(MediaFileNormalizationStatus.AlreadyNormalized, fileResult.Status);
        Assert.Equal(destinationFile, fileResult.DestinationFilePath);
        Assert.Equal("Destination file is already normalized.", fileResult.Message);
        Assert.Equal("tt14452776", fileResult.OmdbEntryId);
        Assert.Equal([17L], result.ObservedFileIds);
        Assert.Equal([23L], result.ObservedTitleIds);
        inventoryProvider.Verify(
            provider => provider.GetAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        imdbClient.Verify(client => client.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<ImdbTitleType?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fileManager.Verify(manager => manager.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Normalize_ProcessesIntakeFilesEvenWhenDestinationInventoryMatches()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var intakeFile = Path.Combine(intakeRoot, "Unsorted.S01E01.mkv");
        var destinationFile = Path.Combine(
            outputRoot,
            "Bob's Burgers",
            "Season 01",
            "Bob's Burgers - S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(intakeRoot)).Returns([intakeFile]);
        fileManager.Setup(manager => manager.FindMediaFiles(outputRoot)).Returns([destinationFile]);
        fileManager.Setup(manager => manager.FileExists(It.IsAny<string>())).Returns(true);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var inventory = new MediaTypeNormalizationInventory(
            [new MediaFile(
                17,
                23,
                1,
                destinationFile,
                destinationFile,
                1,
                1,
                null,
                null,
                "AlreadyNormalized",
                null,
                4,
                4,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true)],
            [new MediaTitle(
                23,
                1,
                "tt14452776",
                "Bob's Burgers",
                2022,
                DateTime.UtcNow,
                DateTime.UtcNow,
                4,
                true)]);
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object,
            CreateInventoryProvider(inventory).Object);

        var result = await handler.Normalize();

        Assert.Contains(result.FileResults, file => file.SourceFilePath == destinationFile
            && file.Status == MediaFileNormalizationStatus.AlreadyNormalized);
        Assert.Contains(result.FileResults, file => file.SourceFilePath == intakeFile
            && file.Status == MediaFileNormalizationStatus.Skipped);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Normalize_ProcessesDestinationFilesWhenInventoryIsStale()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(outputRoot, "Unsorted.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(intakeRoot)).Returns([]);
        fileManager.Setup(manager => manager.FindMediaFiles(outputRoot)).Returns([destinationFile]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var inventory = new MediaTypeNormalizationInventory(
            [new MediaFile(
                17,
                null,
                1,
                destinationFile,
                Path.Combine(outputRoot, "Different.S01E01.mkv"),
                null,
                null,
                null,
                null,
                "AlreadyNormalized",
                null,
                4,
                4,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true)],
            []);
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object,
            CreateInventoryProvider(inventory).Object);

        var result = await handler.Normalize();

        Assert.Equal(MediaFileNormalizationStatus.Skipped, Assert.Single(result.FileResults).Status);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(result.ObservedFileIds);
    }

    [Fact]
    public async Task Normalize_ProcessesDestinationFilesWithoutLinkedTitle()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(outputRoot, "Unsorted.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(intakeRoot)).Returns([]);
        fileManager.Setup(manager => manager.FindMediaFiles(outputRoot)).Returns([destinationFile]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var inventory = new MediaTypeNormalizationInventory(
            [new MediaFile(
                17,
                null,
                1,
                destinationFile,
                destinationFile,
                null,
                null,
                null,
                null,
                "AlreadyNormalized",
                null,
                4,
                4,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true)],
            []);
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object,
            CreateInventoryProvider(inventory).Object);

        var result = await handler.Normalize();

        Assert.Equal(MediaFileNormalizationStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(result.ObservedFileIds);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Normalize_ProcessesDestinationFilesWhenInventoryRowIsInactive()
    {
        var intakeRoot = Path.Combine(Path.GetTempPath(), "EmptyLibrary");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var destinationFile = Path.Combine(outputRoot, "Unsorted.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(intakeRoot)).Returns([]);
        fileManager.Setup(manager => manager.FindMediaFiles(outputRoot)).Returns([destinationFile]);
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync(
                "Unsorted",
                null,
                ImdbTitleType.Series,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1)));
        var inventory = new MediaTypeNormalizationInventory(
            [new MediaFile(
                17,
                null,
                1,
                destinationFile,
                destinationFile,
                null,
                null,
                null,
                null,
                "AlreadyNormalized",
                null,
                4,
                4,
                DateTime.UtcNow,
                DateTime.UtcNow,
                false)],
            []);
        var handler = new TvMediaTypeHandler(
            [intakeRoot],
            outputRoot,
            fileManager.Object,
            imdbClient.Object,
            CreateInventoryProvider(inventory).Object);

        var result = await handler.Normalize();

        Assert.Equal(MediaFileNormalizationStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(result.ObservedFileIds);
        imdbClient.Verify(client => client.SearchAsync(
            "Unsorted",
            null,
            ImdbTitleType.Series,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ITvNormalizationInventoryProvider> CreateInventoryProvider(
        MediaTypeNormalizationInventory inventory)
    {
        var provider = new Mock<ITvNormalizationInventoryProvider>();
        provider
            .Setup(item => item.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory);
        return provider;
    }

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
