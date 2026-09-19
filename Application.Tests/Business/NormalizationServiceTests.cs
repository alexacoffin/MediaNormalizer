using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
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

        await service.NormalizeMediaFiles();

        fileManager.Verify(manager => manager.FindMediaFiles(libraryRoot), Times.Once);
    }
}
