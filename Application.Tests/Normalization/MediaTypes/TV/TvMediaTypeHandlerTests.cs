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

        Assert.Equal([libraryRoot], scannedDirectories);
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
}
