namespace Application.Abstractions.FileSystem;

public interface IFileManager
{
    string[] FindMediaFiles(string directoryPath);

    string? TryReadTextFile(string filePath);

    bool FileExists(string filePath);

    void EnsureDirectory(string directoryPath);

    void MoveFile(string sourceFilePath, string destinationFilePath);

    bool TryDeleteEmptyDirectory(string directoryPath);
}
