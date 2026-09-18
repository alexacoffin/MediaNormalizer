namespace Application.Configuration;

public sealed class FileManagerSettings
{
    public FileManagerSettings(string[] allowedVideoExtensions) =>
        AllowedVideoExtensions = allowedVideoExtensions;

    public string[] AllowedVideoExtensions { get; }

    public static string[] DefaultAllowedVideoExtensions =>
    [
        ".asf", ".avi", ".divx", ".flv",
        ".m2t", ".m2ts", ".mts", ".ts",
        ".m4v", ".mkv", ".mov", ".mp4",
        ".mpeg", ".mpg", ".webm", ".wmv"
    ];
}
