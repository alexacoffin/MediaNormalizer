using Application.Abstractions.FileSystem;
using Application.Configuration;

namespace Infrastructure.FileSystem;

public sealed class FileManager : IFileManager
{
    private readonly HashSet<string> allowedVideoExtensions;

    public FileManager(FileManagerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        allowedVideoExtensions = CreateExtensionSet(settings.AllowedVideoExtensions);

        if (allowedVideoExtensions.Count == 0)
        {
            allowedVideoExtensions = CreateExtensionSet(
                FileManagerSettings.DefaultAllowedVideoExtensions);
        }
    }

    public string[] FindMediaFiles(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var rootDirectory = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The media directory '{rootDirectory}' does not exist.");
        }

        var mediaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directoriesToScan = new Stack<string>();
        directoriesToScan.Push(rootDirectory);

        while (directoriesToScan.TryPop(out var currentDirectory))
        {
            AddMediaFiles(currentDirectory, mediaFiles);
            AddChildDirectories(currentDirectory, directoriesToScan);
        }

        return mediaFiles
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string? TryReadTextFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            return File.ReadAllText(filePath);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public bool FileExists(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.Exists(filePath);
    }

    public void EnsureDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
    }

    public void MoveFile(string sourceFilePath, string destinationFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);
        File.Move(sourceFilePath, destinationFilePath);
    }

    public bool TryDeleteEmptyDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        try
        {
            if (!Directory.Exists(directoryPath)
                || Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                return false;
            }

            Directory.Delete(directoryPath, false);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static HashSet<string> CreateExtensionSet(IEnumerable<string>? extensions)
    {
        var extensionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (extensions is null)
        {
            return extensionSet;
        }

        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            var normalizedExtension = extension.Trim();
            if (!normalizedExtension.StartsWith('.'))
            {
                normalizedExtension = $".{normalizedExtension}";
            }

            extensionSet.Add(normalizedExtension);
        }

        return extensionSet;
    }

    private void AddMediaFiles(string directoryPath, HashSet<string> mediaFiles)
    {
        try
        {
            foreach (var filePath in Directory.GetFiles(directoryPath))
            {
                if (allowedVideoExtensions.Contains(Path.GetExtension(filePath)))
                {
                    mediaFiles.Add(Path.GetFullPath(filePath));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static void AddChildDirectories(
        string directoryPath,
        Stack<string> directoriesToScan)
    {
        string[] childDirectories;

        try
        {
            childDirectories = Directory.GetDirectories(directoryPath);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var childDirectory in childDirectories)
        {
            try
            {
                if (!File.GetAttributes(childDirectory).HasFlag(FileAttributes.ReparsePoint))
                {
                    directoriesToScan.Push(childDirectory);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
        }
    }
}
