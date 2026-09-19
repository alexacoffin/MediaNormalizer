using Application.Abstractions.FileSystem;
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
}
