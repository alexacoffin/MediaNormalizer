using Application.Abstractions.FileSystem;

namespace Application.Tests.TestDoubles;

internal sealed class StubFileManager : IFileManager
{
    public Func<string, string?>? TextFileReader { get; init; }

    public string[] FindMediaFiles(string directoryPath) => [];

    public string? TryReadTextFile(string filePath) =>
        TextFileReader?.Invoke(filePath);

    public bool FileExists(string filePath) => false;

    public void EnsureDirectory(string directoryPath)
    {
    }

    public void MoveFile(string sourceFilePath, string destinationFilePath)
    {
    }

    public bool TryDeleteEmptyDirectory(string directoryPath) => false;
}
