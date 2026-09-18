using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeHandlerTests
{
    [Fact]
    public async Task Normalize_DiscoversAndIdentifiesEachShowFolder()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var firstEpisode = Path.Combine(libraryRoot, "The Bear", "Season 01", "The.Bear.S01E01.mkv");
        var secondEpisode = Path.Combine(libraryRoot, "The Bear", "Season 02", "The.Bear.S02E01.mkv");
        var fileManager = new FakeFileManager
        {
            FilesByDirectory = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [libraryRoot] = [firstEpisode, secondEpisode]
            }
        };
        var imdbClient = new FakeImdbClient
        {
            SearchResponse = ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(
                [new ImdbTitleSummary("tt14452776", "The Bear", "2022", ImdbTitleType.Series)],
                1,
                1))
        };
        var handler = new TvMediaTypeHandler([libraryRoot], outputRoot, fileManager, imdbClient);

        var formattingResult = await handler.NormalizeAsync();

        Assert.Equal([libraryRoot], fileManager.ScannedDirectories);
        Assert.Equal(2, formattingResult.RenamedCount);
        Assert.All(
            formattingResult.FileResults,
            result => Assert.StartsWith(outputRoot, result.DestinationFilePath!, StringComparison.OrdinalIgnoreCase));
        var search = Assert.Single(imdbClient.Searches);
        Assert.Equal("The Bear", search.Title);
        Assert.Equal(ImdbTitleType.Series, search.Type);
    }

    private sealed class FakeFileManager : IFileManager
    {
        public Dictionary<string, string[]> FilesByDirectory { get; init; } = [];

        public List<string> ScannedDirectories { get; } = [];

        public string[] FindMediaFiles(string directoryPath)
        {
            ScannedDirectories.Add(directoryPath);
            return FilesByDirectory.TryGetValue(directoryPath, out var filePaths)
                ? filePaths
                : [];
        }

        public string? TryReadTextFile(string filePath) => null;

        public bool FileExists(string filePath) => false;

        public void EnsureDirectory(string directoryPath)
        {
        }

        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
        }

        public bool TryDeleteEmptyDirectory(string directoryPath) => false;
    }

    private sealed class FakeImdbClient : IImdbClient
    {
        public ImdbResult<ImdbSearchPage> SearchResponse { get; init; } =
            ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage([], 0, 1));

        public List<SearchRequest> Searches { get; } = [];

        public Task<ImdbResult<ImdbSearchPage>> SearchAsync(
            string title,
            int? year = null,
            ImdbTitleType? type = null,
            int page = 1,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(new SearchRequest(title, year, type, page));
            return Task.FromResult(SearchResponse);
        }

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
