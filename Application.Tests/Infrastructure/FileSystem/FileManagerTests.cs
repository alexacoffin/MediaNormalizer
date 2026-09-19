using Application.Configuration;
using Infrastructure.FileSystem;
using Xunit;

namespace Application.Tests.Infrastructure.FileSystem;

public sealed class FileManagerTests
{
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
}
