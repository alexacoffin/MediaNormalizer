using Application.Configuration;
using Infrastructure.FileSystem;
using Xunit;

namespace Application.Tests.Infrastructure.FileSystem;

public sealed class FileManagerTests
{
    [Fact]
    public void FindMediaFiles_RecursivelyFiltersNormalizesAndSortsVideoFiles()
    {
        var rootDirectory = CreateTempDirectory();
        var nestedDirectory = Path.Combine(rootDirectory, "Season 01", "Nested");
        Directory.CreateDirectory(nestedDirectory);
        var expectedFiles = new[]
        {
            Path.Combine(rootDirectory, "z.MKV"),
            Path.Combine(rootDirectory, "Season 01", "a.mp4")
        };
        File.WriteAllText(expectedFiles[0], string.Empty);
        File.WriteAllText(expectedFiles[1], string.Empty);
        File.WriteAllText(Path.Combine(nestedDirectory, "ignored.txt"), string.Empty);
        File.WriteAllText(Path.Combine(rootDirectory, "ignored.avi"), string.Empty);

        try
        {
            var fileManager = new FileManager(new FileManagerSettings(["mkv", " .MP4 "]));

            var mediaFiles = fileManager.FindMediaFiles(rootDirectory);

            Assert.Equal(
                expectedFiles
                    .Select(Path.GetFullPath)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
                mediaFiles);
            Assert.Equal(mediaFiles.Length, mediaFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public void FindMediaFiles_ThrowsWhenRootDoesNotExist()
    {
        var fileManager = new FileManager(new FileManagerSettings([]));
        var missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(
            () => fileManager.FindMediaFiles(missingDirectory));
    }

    [Fact]
    public void EnsureDirectoryAndMoveFile_CreateDestinationAndPreserveContent()
    {
        var rootDirectory = CreateTempDirectory();
        var sourcePath = Path.Combine(rootDirectory, "source.mkv");
        var destinationDirectory = Path.Combine(rootDirectory, "normalized", "Season 01");
        var destinationPath = Path.Combine(destinationDirectory, "episode.mkv");
        File.WriteAllText(sourcePath, "episode content");

        try
        {
            var fileManager = new FileManager(new FileManagerSettings([]));

            fileManager.EnsureDirectory(destinationDirectory);
            fileManager.MoveFile(sourcePath, destinationPath);

            Assert.False(File.Exists(sourcePath));
            Assert.True(File.Exists(destinationPath));
            Assert.Equal("episode content", File.ReadAllText(destinationPath));
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public void MoveFile_ThrowsAndLeavesBothFilesWhenDestinationExists()
    {
        var rootDirectory = CreateTempDirectory();
        var sourcePath = Path.Combine(rootDirectory, "source.mkv");
        var destinationPath = Path.Combine(rootDirectory, "destination.mkv");
        File.WriteAllText(sourcePath, "source content");
        File.WriteAllText(destinationPath, "destination content");

        try
        {
            var fileManager = new FileManager(new FileManagerSettings([]));

            Assert.Throws<IOException>(() => fileManager.MoveFile(sourcePath, destinationPath));
            Assert.Equal("source content", File.ReadAllText(sourcePath));
            Assert.Equal("destination content", File.ReadAllText(destinationPath));
        }
        finally
        {
            DeleteDirectory(rootDirectory);
        }
    }

    [Fact]
    public void FileOperations_RejectNullAndWhitespacePaths()
    {
        var fileManager = new FileManager(new FileManagerSettings([]));
        var sourcePath = Path.Combine(Path.GetTempPath(), "source.mkv");
        var destinationPath = Path.Combine(Path.GetTempPath(), "destination.mkv");

        Assert.Throws<ArgumentNullException>(() => fileManager.FindMediaFiles(null!));
        Assert.Throws<ArgumentException>(() => fileManager.FindMediaFiles(" "));
        Assert.Throws<ArgumentNullException>(() => fileManager.EnsureDirectory(null!));
        Assert.Throws<ArgumentException>(() => fileManager.EnsureDirectory(" "));
        Assert.Throws<ArgumentNullException>(() => fileManager.MoveFile(null!, destinationPath));
        Assert.Throws<ArgumentException>(() => fileManager.MoveFile(sourcePath, " "));
    }

    [Fact]
    public void TryReadTextFile_ReturnsContentsForAnExistingFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nfo");
        File.WriteAllText(filePath, "<tvshow><title>Bob's Burgers</title></tvshow>");

        try
        {
            var fileManager = new FileManager(new FileManagerSettings([]));

            var content = fileManager.TryReadTextFile(filePath);

            Assert.Equal("<tvshow><title>Bob's Burgers</title></tvshow>", content);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void TryReadTextFile_ReturnsNullForAMissingFile()
    {
        var fileManager = new FileManager(new FileManagerSettings([]));

        var content = fileManager.TryReadTextFile(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nfo"));

        Assert.Null(content);
    }

    [Fact]
    public void TryDeleteEmptyDirectory_DeletesOnlyAnEmptyDirectory()
    {
        var emptyDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var nonEmptyDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDirectory);
        Directory.CreateDirectory(nonEmptyDirectory);
        File.WriteAllText(Path.Combine(nonEmptyDirectory, "episode.mkv"), string.Empty);

        try
        {
            var fileManager = new FileManager(new FileManagerSettings([]));

            Assert.True(fileManager.TryDeleteEmptyDirectory(emptyDirectory));
            Assert.False(Directory.Exists(emptyDirectory));
            Assert.False(fileManager.TryDeleteEmptyDirectory(nonEmptyDirectory));
            Assert.True(Directory.Exists(nonEmptyDirectory));
        }
        finally
        {
            if (Directory.Exists(emptyDirectory))
            {
                Directory.Delete(emptyDirectory, true);
            }

            if (Directory.Exists(nonEmptyDirectory))
            {
                Directory.Delete(nonEmptyDirectory, true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "MediaNormalizerTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
