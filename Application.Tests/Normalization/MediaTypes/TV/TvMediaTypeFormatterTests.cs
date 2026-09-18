using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeFormatterTests
{
    [Fact]
    public async Task FormatAsync_MovesEpisodeToCanonicalPathWithEpisodeTitle()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "The Bear", "The.Bear.S01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var imdbClient = new FakeImdbClient
        {
            EpisodeResponse = ImdbResult<ImdbTitleDetails>.Success(
                new ImdbTitleDetails("tt2000001", "Hands", "2022", ImdbTitleType.Episode, "tt14452776", 1, 2))
        };
        var formatter = new TvMediaTypeFormatter(fileManager, imdbClient, outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var expectedPath = Path.Combine(
            outputRoot,
            "The Bear (2022)",
            "Season 01",
            "The Bear (2022) - S01E02 - Hands.mkv");
        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(TvMediaTypeFormattingStatus.Renamed, fileResult.Status);
        Assert.Equal(expectedPath, fileResult.DestinationFilePath);
        Assert.Contains(expectedPath, fileManager.Files, StringComparer.OrdinalIgnoreCase);
        Assert.Single(imdbClient.EpisodeRequests);
        Assert.Equal(
            [Path.Combine(rootPath, "The Bear")],
            fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_UsesEpisodeCodeWhenEpisodeLookupFails()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "The Bear", "The.Bear.S01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(fileManager, new FakeImdbClient(), outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(TvMediaTypeFormattingStatus.Renamed, fileResult.Status);
        Assert.EndsWith("The Bear (2022) - S01E02.mkv", fileResult.DestinationFilePath);
    }

    [Fact]
    public async Task FormatAsync_FormatsAirDateWithoutEpisodeLookup()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Daily Show", "The.Daily.Show.2024-06-26.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var imdbClient = new FakeImdbClient();
        var formatter = new TvMediaTypeFormatter(fileManager, imdbClient, outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath, "The Daily Show"));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(TvMediaTypeFormattingStatus.Renamed, fileResult.Status);
        Assert.Equal(
            Path.Combine(outputRoot, "The Daily Show (2022)", "The Daily Show (2022) - 2024-06-26.mkv"),
            fileResult.DestinationFilePath);
        Assert.Empty(imdbClient.EpisodeRequests);
    }

    [Fact]
    public async Task FormatAsync_SkipsWhenDestinationFileExists()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "The Bear", "The.Bear.S01E02.mkv");
        var destinationPath = Path.Combine(outputRoot, "The Bear (2022)", "Season 01", "The Bear (2022) - S01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath, destinationPath]);
        var formatter = new TvMediaTypeFormatter(fileManager, new FakeImdbClient(), outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(TvMediaTypeFormattingStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_SkipsMultiEpisodeFiles()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "The Bear", "The.Bear.S01E01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(fileManager, new FakeImdbClient(), outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(TvMediaTypeFormattingStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_WhenSourceAlreadyUsesOutputPath_ReturnsAlreadyNormalized()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(
            outputRoot,
            "The Bear (2022)",
            "Season 01",
            "The Bear (2022) - S01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(fileManager, new FakeImdbClient(), outputRoot);

        var result = await formatter.FormatAsync(
            CreateIdentificationResult(
                outputRoot,
                sourcePath,
                "The Bear"));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(TvMediaTypeFormattingStatus.AlreadyNormalized, fileResult.Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_DoesNotCleanOutputDirectoriesNestedUnderIntakeRoot()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(rootPath, "FormattedTV");
        var sourcePath = Path.Combine(outputRoot, "The Bear", "The.Bear.S01E02.mkv");
        var fileManager = new FakeFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(fileManager, new FakeImdbClient(), outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(TvMediaTypeFormattingStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    private static TvIdentificationRunResult CreateIdentificationResult(
        string rootPath,
        string sourcePath,
        string seriesTitle = "The Bear")
    {
        var group = new TvShowFolderGroup(
            rootPath,
            Path.GetDirectoryName(sourcePath)!,
            seriesTitle,
            [sourcePath]);
        var series = new ImdbTitleSummary("tt14452776", seriesTitle, "2022–", ImdbTitleType.Series);
        var attempt = new TvShowMatchAttempt(
            TvShowEvidenceSource.FolderName,
            seriesTitle,
            2022,
            TvShowIdentificationStatus.Matched,
            series,
            null);
        return new TvIdentificationRunResult([new TvShowIdentification(group, null, [attempt])], []);
    }

    private sealed class FakeFileManager : IFileManager
    {
        public FakeFileManager(IEnumerable<string> files) =>
            Files = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Files { get; }

        public List<(string Source, string Destination)> Moves { get; } = [];

        public List<string> DeletedDirectories { get; } = [];

        public string[] FindMediaFiles(string directoryPath) => [];

        public string? TryReadTextFile(string filePath) => null;

        public bool FileExists(string filePath) => Files.Contains(filePath);

        public void EnsureDirectory(string directoryPath)
        {
        }

        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
            Assert.True(Files.Remove(sourceFilePath));
            Assert.True(Files.Add(destinationFilePath));
            Moves.Add((sourceFilePath, destinationFilePath));
        }

        public bool TryDeleteEmptyDirectory(string directoryPath)
        {
            DeletedDirectories.Add(directoryPath);
            return true;
        }
    }

    private sealed class FakeImdbClient : IImdbClient
    {
        public ImdbResult<ImdbTitleDetails> EpisodeResponse { get; init; } =
            ImdbResult<ImdbTitleDetails>.Failure(new ImdbError(ImdbErrorKind.NotFound, "Not found."));

        public List<string> EpisodeRequests { get; } = [];

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
            CancellationToken cancellationToken = default)
        {
            EpisodeRequests.Add($"{seriesImdbId}|{seasonNumber}|{episodeNumber}");
            return Task.FromResult(EpisodeResponse);
        }
    }
}
