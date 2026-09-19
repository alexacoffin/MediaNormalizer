using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Normalization;
using Application.Normalization.MediaTypes.TV;
using Application.Normalization.MediaTypes.TV.Internals;
using Application.Normalization.MediaTypes.TV.Internals.Enums;
using Moq;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvMediaTypeFormatterTests
{
    [Fact]
    public async Task FormatAsync_MovesEpisodeToCanonicalPathWithEpisodeTitle()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var imdbClient = CreateImdbClient(
            ImdbResult<ImdbTitleDetails>.Success(
                new ImdbTitleDetails("tt2000001", "Hands", "2022", ImdbTitleType.Episode, "tt14452776", 1, 2)));
        var formatter = new TvMediaTypeFormatter(fileManager.Mock.Object, imdbClient.Mock.Object, outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var expectedPath = Path.Combine(
            outputRoot,
            "Bob's Burgers (2022)",
            "Season 01",
            "Bob's Burgers (2022) - S01E02 - Hands.mkv");
        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Renamed, fileResult.Status);
        Assert.Equal(expectedPath, fileResult.DestinationFilePath);
        Assert.Contains(expectedPath, fileManager.FilePaths, StringComparer.OrdinalIgnoreCase);
        Assert.Single(imdbClient.EpisodeRequests);
        Assert.Equal(
            [Path.Combine(rootPath, "Bob's Burgers")],
            fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_UsesEpisodeCodeWhenEpisodeLookupFails()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Renamed, fileResult.Status);
        Assert.EndsWith("Bob's Burgers (2022) - S01E02.mkv", fileResult.DestinationFilePath);
    }

    [Fact]
    public async Task FormatAsync_FormatsAirDateWithoutEpisodeLookup()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Daily Show", "The.Daily.Show.2024-06-26.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var imdbClient = CreateImdbClient(null);
        var formatter = new TvMediaTypeFormatter(fileManager.Mock.Object, imdbClient.Mock.Object, outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath, "The Daily Show"));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Renamed, fileResult.Status);
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
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var destinationPath = Path.Combine(outputRoot, "Bob's Burgers (2022)", "Season 01", "Bob's Burgers (2022) - S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath, destinationPath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_SkipsMultiEpisodeFiles()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Skipped, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_WhenSourceAlreadyUsesOutputPath_ReturnsAlreadyNormalized()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(
            outputRoot,
            "Bob's Burgers (2022)",
            "Season 01",
            "Bob's Burgers (2022) - S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(
            CreateIdentificationResult(
                outputRoot,
                sourcePath,
            "Bob's Burgers"));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.AlreadyNormalized, fileResult.Status);
        Assert.Empty(fileManager.Moves);
    }

    [Fact]
    public async Task FormatAsync_DoesNotCleanOutputDirectoriesNestedUnderIntakeRoot()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "Library");
        var outputRoot = Path.Combine(rootPath, "FormattedTV");
        var sourcePath = Path.Combine(outputRoot, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(IOException))]
    public async Task FormatAsync_ReturnsFailedWhenMoveThrows(Type exceptionType)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager(
            [sourcePath],
            (Exception)Activator.CreateInstance(exceptionType, "Move failed.")!);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Failed, fileResult.Status);
        Assert.Equal("Move failed.", fileResult.Message);
        Assert.Empty(fileManager.Moves);
        Assert.Contains(
            Path.Combine(outputRoot, "Bob's Burgers (2022)", "Season 01"),
            fileManager.EnsuredDirectories,
            StringComparer.OrdinalIgnoreCase);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_ReturnsFailedWhenSeriesTitleCannotBeUsedAsAFileName()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(
            CreateIdentificationResult(rootPath, sourcePath, seriesTitle: "..."));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Failed, fileResult.Status);
        Assert.Null(fileResult.DestinationFilePath);
        Assert.Empty(fileManager.Moves);
        Assert.Empty(fileManager.EnsuredDirectories);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Theory]
    [InlineData((int)TvShowIdentificationStatus.NoExactMatch)]
    [InlineData((int)TvShowIdentificationStatus.AmbiguousExactMatches)]
    public async Task FormatAsync_SkipsFilesWhenSeriesWasNotMatched(
        int identificationStatus)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(
            CreateUnmatchedIdentificationResult(
                rootPath,
                sourcePath,
                (TvShowIdentificationStatus)identificationStatus));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Skipped, fileResult.Status);
        Assert.Equal("The series was not matched with confidence.", fileResult.Message);
        Assert.Empty(fileManager.Moves);
        Assert.Empty(fileManager.EnsuredDirectories);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_FormatsLooseFileIdentification()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(
            CreateLooseIdentificationResult(rootPath, sourcePath));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Renamed, fileResult.Status);
        Assert.Contains("Bob's Burgers (2022) - S01E02.mkv", fileResult.DestinationFilePath);
        Assert.Single(fileManager.Moves);
        Assert.Single(fileManager.DeletedDirectories);
        Assert.Single(result.DeletedDirectories);
        Assert.Equal(
            Path.Combine(rootPath, "Bob's Burgers"),
            fileManager.DeletedDirectories[0]);
        Assert.Equal(
            Path.Combine(rootPath, "Bob's Burgers"),
            result.DeletedDirectories[0]);
    }

    [Fact]
    public async Task FormatAsync_DoesNotReportDirectoriesThatWereNotDeleted()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath], deleteDirectoryResult: false);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(result.DeletedDirectories);
        Assert.Single(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_SkipsFilesWithoutAnEpisodeMarker()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        var fileResult = Assert.Single(result.FileResults);
        Assert.Equal(MediaFileNormalizationStatus.Skipped, fileResult.Status);
        Assert.Equal("No supported episode marker was found.", fileResult.Message);
        Assert.Empty(fileManager.Moves);
        Assert.Empty(fileManager.EnsuredDirectories);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_DoesNotCleanDirectoriesWhenSourceIsOutsideTvRoot()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(Path.GetTempPath(), "Other", "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_DoesNotCleanTvRootDirectoryWhenSourceFileIsDirectlyUnderIt()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "TV");
        var outputRoot = Path.Combine(Path.GetTempPath(), "FormattedTV");
        var sourcePath = Path.Combine(rootPath, "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    [Fact]
    public async Task FormatAsync_DoesNotCleanWhenOutputDirectoryContainsTvRoot()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "Library");
        var rootPath = Path.Combine(outputRoot, "TV");
        var sourcePath = Path.Combine(rootPath, "Bob's Burgers", "Bob's.Burgers.S01E02.mkv");
        var fileManager = CreateFileManager([sourcePath]);
        var formatter = new TvMediaTypeFormatter(
            fileManager.Mock.Object,
            CreateImdbClient(null).Mock.Object,
            outputRoot);

        var result = await formatter.FormatAsync(CreateIdentificationResult(rootPath, sourcePath));

        Assert.Equal(MediaFileNormalizationStatus.Renamed, Assert.Single(result.FileResults).Status);
        Assert.Empty(fileManager.DeletedDirectories);
    }

    private static TvIdentificationRunResult CreateIdentificationResult(
        string rootPath,
        string sourcePath,
        string seriesTitle = "Bob's Burgers")
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

    private static TvIdentificationRunResult CreateUnmatchedIdentificationResult(
        string rootPath,
        string sourcePath,
        TvShowIdentificationStatus identificationStatus)
    {
        var group = new TvShowFolderGroup(
            rootPath,
            Path.GetDirectoryName(sourcePath)!,
            "Bob's Burgers",
            [sourcePath]);
        var attempt = new TvShowMatchAttempt(
            TvShowEvidenceSource.FolderName,
            "Bob's Burgers",
            null,
            identificationStatus,
            null,
            null);
        return new TvIdentificationRunResult(
            [new TvShowIdentification(group, null, [attempt])],
            []);
    }

    private static TvIdentificationRunResult CreateLooseIdentificationResult(
        string rootPath,
        string sourcePath)
    {
        var group = new TvUnsupportedFileGroup(rootPath, [sourcePath]);
        var seriesTitle = "Bob's Burgers";
        var series = new ImdbTitleSummary("tt14452776", seriesTitle, "2022–", ImdbTitleType.Series);
        var attempt = new TvShowMatchAttempt(
            TvShowEvidenceSource.Filename,
            seriesTitle,
            null,
            TvShowIdentificationStatus.Matched,
            series,
            null);
        return new TvIdentificationRunResult(
            [],
            [new TvShowIdentification(null, group, [attempt])]);
    }

    private static FileManagerFixture CreateFileManager(
        IEnumerable<string> files,
        Exception? moveException = null,
        bool deleteDirectoryResult = true)
    {
        var fixture = new FileManagerFixture(files);
        fixture.Mock
            .Setup(manager => manager.FileExists(It.IsAny<string>()))
            .Returns((string filePath) => fixture.FilePaths.Contains(filePath));
        fixture.Mock
            .Setup(manager => manager.EnsureDirectory(It.IsAny<string>()))
            .Callback<string>(directoryPath => fixture.EnsuredDirectories.Add(directoryPath));
        fixture.Mock
            .Setup(manager => manager.MoveFile(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((sourceFilePath, destinationFilePath) =>
            {
                if (moveException is not null)
                {
                    throw moveException;
                }

                Assert.True(fixture.FilePaths.Remove(sourceFilePath));
                Assert.True(fixture.FilePaths.Add(destinationFilePath));
                fixture.Moves.Add((sourceFilePath, destinationFilePath));
            });
        fixture.Mock
            .Setup(manager => manager.TryDeleteEmptyDirectory(It.IsAny<string>()))
            .Callback<string>(directoryPath => fixture.DeletedDirectories.Add(directoryPath))
            .Returns(deleteDirectoryResult);

        return fixture;
    }

    private static ImdbClientFixture CreateImdbClient(ImdbResult<ImdbTitleDetails>? episodeResponse)
    {
        var fixture = new ImdbClientFixture(episodeResponse);
        fixture.Mock
            .Setup(client => client.GetEpisodeAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>(
                (seriesImdbId, seasonNumber, episodeNumber, _) =>
                    fixture.EpisodeRequests.Add($"{seriesImdbId}|{seasonNumber}|{episodeNumber}"))
            .ReturnsAsync(fixture.EpisodeResponse);

        return fixture;
    }

    private sealed class FileManagerFixture
    {
        public FileManagerFixture(IEnumerable<string> files)
        {
            FilePaths = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        }

        public Mock<IFileManager> Mock { get; } = new();

        public HashSet<string> FilePaths { get; }

        public List<(string Source, string Destination)> Moves { get; } = [];

        public List<string> EnsuredDirectories { get; } = [];

        public List<string> DeletedDirectories { get; } = [];
    }

    private sealed class ImdbClientFixture
    {
        public ImdbClientFixture(ImdbResult<ImdbTitleDetails>? episodeResponse)
        {
            EpisodeResponse = episodeResponse ?? ImdbResult<ImdbTitleDetails>.Failure(
                new ImdbError(ImdbErrorKind.NotFound, "Not found."));
        }

        public Mock<IImdbClient> Mock { get; } = new();

        public ImdbResult<ImdbTitleDetails> EpisodeResponse { get; }

        public List<string> EpisodeRequests { get; } = [];
    }
}
