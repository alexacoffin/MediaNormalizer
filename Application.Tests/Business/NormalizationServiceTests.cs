using Application.Abstractions.FileSystem;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization;
using Business.Services;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.Tests.Business;

public sealed class NormalizationServiceTests
{
    [Fact]
    public async Task NormalizeMediaFiles_PersistsRunAndTvResults()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(libraryRoot, "Bob's Burgers", "Bob's.Burgers.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(It.IsAny<string>())).Returns([sourcePath]);
        fileManager.Setup(manager => manager.FileExists(It.IsAny<string>())).Returns(false);
        fileManager.Setup(manager => manager.TryDeleteEmptyDirectory(It.IsAny<string>())).Returns(false);
        var imdbClient = CreateBobBurgersClient();
        var runs = new Mock<INormalizationRunsRepository>();
        runs.Setup(repository => repository.UpsertAsync(It.IsAny<NormalizationRunUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationRunUpsert request, CancellationToken _) =>
                new NormalizationRun(request.Id ?? 42, DateTime.UtcNow, request.CompletedAtUtc, request.Status, request.ErrorMessage));
        var titles = new Mock<IMediaTitlesRepository>();
        titles.Setup(repository => repository.UpsertAsync(It.IsAny<MediaTitleUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaTitle(5, 1, "tt14452776", "Bob's Burgers", 2022, DateTime.UtcNow, DateTime.UtcNow, 42, true));
        titles.Setup(repository => repository.GetActiveByMediaTypeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var files = new Mock<IMediaFilesRepository>();
        files.Setup(repository => repository.GetByCurrentPathAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFile?)null);
        files.Setup(repository => repository.UpsertAsync(It.IsAny<MediaFileUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaFileUpsert request, CancellationToken _) =>
                new MediaFile(7, request.TitleId, request.MediaTypeId, request.CurrentPath, request.CanonicalPath, request.SeasonNumber, request.EpisodeNumber, request.AirDate, request.EpisodeTitle, request.LastStatus, request.LastMessage, request.FirstSeenRunId, request.LastSeenRunId, DateTime.UtcNow, DateTime.UtcNow, true));
        files.Setup(repository => repository.GetActiveByMediaTypeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var fileResults = new Mock<INormalizationFileResultsRepository>();
        fileResults.Setup(repository => repository.UpsertAsync(It.IsAny<NormalizationFileResultUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NormalizationFileResult(8, 42, 7, 5, 1, sourcePath, Path.Combine(outputRoot, "Bob's Burgers (2022)", "Season 01", "Bob's Burgers (2022) - S01E01.mkv"), "Renamed", "formatted", "Intake", DateTime.UtcNow));
        var deletedDirectories = new Mock<INormalizationDeletedDirectoriesRepository>();

        var service = new NormalizationService(
            CreateLibraryRequest(libraryRoot, outputRoot),
            new MediaTypeHandler(fileManager.Object, imdbClient.Object),
            runs.Object,
            titles.Object,
            files.Object,
            fileResults.Object,
            deletedDirectories.Object);

        var result = await service.NormalizeMediaFiles();

        Assert.Equal(1, result.Renamed.Count);
        runs.Verify(repository => repository.UpsertAsync(It.Is<NormalizationRunUpsert>(request => request.Status == "Running"), It.IsAny<CancellationToken>()), Times.Once);
        runs.Verify(repository => repository.UpsertAsync(It.Is<NormalizationRunUpsert>(request => request.Id == 42 && request.Status == "Completed"), It.IsAny<CancellationToken>()), Times.Once);
        titles.Verify(repository => repository.UpsertAsync(It.Is<MediaTitleUpsert>(request => request.OmdbEntryId == "tt14452776" && request.LastSeenRunId == 42), It.IsAny<CancellationToken>()), Times.Once);
        files.Verify(repository => repository.UpsertAsync(It.Is<MediaFileUpsert>(request => request.TitleId == 5 && request.LastStatus == "Renamed" && request.LastSeenRunId == 42), It.IsAny<CancellationToken>()), Times.Once);
        fileResults.Verify(repository => repository.UpsertAsync(It.Is<NormalizationFileResultUpsert>(request => request.NormalizationRunId == 42 && request.MediaFileId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NormalizeMediaFiles_ReturnsFilesystemResultWhenDatabaseStartupFails()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(It.IsAny<string>())).Returns([]);
        var runs = new Mock<INormalizationRunsRepository>();
        runs.Setup(repository => repository.UpsertAsync(It.IsAny<NormalizationRunUpsert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var service = new NormalizationService(
            new MediaLibraryNormalizationRequest(
                [libraryRoot],
                [new MediaTypeNormalizationRequest(MediaType.Tv, "TV", "TV", Path.Combine(Path.GetTempPath(), "TV"), true)]),
            new MediaTypeHandler(fileManager.Object, new Mock<IImdbClient>().Object),
            runs.Object,
            new Mock<IMediaTitlesRepository>().Object,
            new Mock<IMediaFilesRepository>().Object,
            new Mock<INormalizationFileResultsRepository>().Object,
            new Mock<INormalizationDeletedDirectoriesRepository>().Object);

        var result = await service.NormalizeMediaFiles();

        Assert.Equal("completed", result.Status);
        Assert.Empty(result.Renamed.Paths);
    }

    [Fact]
    public async Task NormalizeMediaFiles_DoesNotReconcileWhenTvIsDisabled()
    {
        var fileManager = new Mock<IFileManager>();
        var runs = CreateSuccessfulRunsRepository();
        var titles = new Mock<IMediaTitlesRepository>();
        var files = new Mock<IMediaFilesRepository>();
        titles.Setup(repository => repository.GetActiveByMediaTypeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MediaTitle(1, 1, "tt1", "Example", 2020, DateTime.UtcNow, DateTime.UtcNow, 1, true)]);
        files.Setup(repository => repository.GetActiveByMediaTypeAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MediaFile(2, 1, 1, "z:\\example.mkv", null, null, null, null, null, null, null, 1, 1, DateTime.UtcNow, DateTime.UtcNow, true)]);

        var service = new NormalizationService(
            new MediaLibraryNormalizationRequest(
                [],
                [new MediaTypeNormalizationRequest(MediaType.Tv, "TV", "TV", "z:\\tv", false)]),
            new MediaTypeHandler(fileManager.Object, new Mock<IImdbClient>().Object),
            runs.Object,
            titles.Object,
            files.Object,
            new Mock<INormalizationFileResultsRepository>().Object,
            new Mock<INormalizationDeletedDirectoriesRepository>().Object);

        await service.NormalizeMediaFiles();

        files.Verify(repository => repository.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        titles.Verify(repository => repository.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NormalizeMediaFiles_MarksRunFailedWhenHandlerThrows()
    {
        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(manager => manager.FindMediaFiles(It.IsAny<string>()))
            .Throws<DirectoryNotFoundException>();
        var runs = CreateSuccessfulRunsRepository();
        var service = new NormalizationService(
            new MediaLibraryNormalizationRequest(
                ["z:\\intake"],
                [new MediaTypeNormalizationRequest(MediaType.Tv, "TV", "TV", "z:\\tv", true)]),
            new MediaTypeHandler(fileManager.Object, new Mock<IImdbClient>().Object),
            runs.Object,
            new Mock<IMediaTitlesRepository>().Object,
            new Mock<IMediaFilesRepository>().Object,
            new Mock<INormalizationFileResultsRepository>().Object,
            new Mock<INormalizationDeletedDirectoriesRepository>().Object);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.NormalizeMediaFiles());

        runs.Verify(repository => repository.UpsertAsync(It.Is<NormalizationRunUpsert>(request => request.Id == 42 && request.Status == "Failed"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NormalizeMediaFiles_DispatchesConfiguredTvMediaType()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(libraryRoot))
            .Returns([]);
        var mediaTypeHandler = new MediaTypeHandler(
            fileManager.Object,
            new Mock<IImdbClient>().Object);
        var service = new NormalizationService(
            new MediaLibraryNormalizationRequest(
                [libraryRoot],
                [new MediaTypeNormalizationRequest(
                    MediaType.Tv,
                    "TV",
                    "TV",
                    Path.Combine(Path.GetTempPath(), "FormattedTV"),
                    enabled: true)]),
            mediaTypeHandler);

        var result = await service.NormalizeMediaFiles();

        fileManager.Verify(manager => manager.FindMediaFiles(libraryRoot), Times.Once);
        Assert.Empty(result.Renamed.Paths);
        Assert.Empty(result.DeletedDirectories.Paths);
    }

    [Fact]
    public async Task NormalizeMediaFiles_ReturnsResultsFromTvHandler()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(libraryRoot, "Bob's Burgers", "Bob's.Burgers.S01E01.mkv");
        var fileManager = new Mock<IFileManager>();
        fileManager
            .Setup(manager => manager.FindMediaFiles(libraryRoot))
            .Returns([sourcePath]);
        fileManager
            .Setup(manager => manager.FileExists(It.IsAny<string>()))
            .Returns(false);
        fileManager
            .Setup(manager => manager.TryDeleteEmptyDirectory(It.IsAny<string>()))
            .Returns(true);
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
        imdbClient
            .Setup(client => client.GetEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(
                new ImdbError(ImdbErrorKind.NotFound, "Not found.")));
        var service = new NormalizationService(
            new MediaLibraryNormalizationRequest(
                [libraryRoot],
                [new MediaTypeNormalizationRequest(
                    MediaType.Tv,
                    "TV",
                    "TV",
                    outputRoot,
                    enabled: true)]),
            new MediaTypeHandler(fileManager.Object, imdbClient.Object));

        var result = await service.NormalizeMediaFiles();

        Assert.Equal(1, result.Renamed.Count);
        Assert.StartsWith(outputRoot, Assert.Single(result.Renamed.Paths), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            Path.Combine(libraryRoot, "Bob's Burgers"),
            result.DeletedDirectories.Paths,
            StringComparer.OrdinalIgnoreCase);
    }

    private static MediaLibraryNormalizationRequest CreateLibraryRequest(string libraryRoot, string outputRoot) =>
        new(
            [libraryRoot],
            [new MediaTypeNormalizationRequest(MediaType.Tv, "TV", "TV", outputRoot, true)]);

    private static Mock<IImdbClient> CreateBobBurgersClient()
    {
        var imdbClient = new Mock<IImdbClient>();
        imdbClient
            .Setup(client => client.SearchAsync("Bob's Burgers", null, ImdbTitleType.Series, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "Bob's Burgers", "2022", ImdbTitleType.Series)], 1, 1)));
        imdbClient
            .Setup(client => client.GetEpisodeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(ImdbErrorKind.NotFound, "Not found.")));
        return imdbClient;
    }

    private static Mock<INormalizationRunsRepository> CreateSuccessfulRunsRepository()
    {
        var runs = new Mock<INormalizationRunsRepository>();
        runs.Setup(repository => repository.UpsertAsync(It.IsAny<NormalizationRunUpsert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NormalizationRunUpsert request, CancellationToken _) =>
                new NormalizationRun(request.Id ?? 42, DateTime.UtcNow, request.CompletedAtUtc, request.Status, request.ErrorMessage));
        return runs;
    }
}
