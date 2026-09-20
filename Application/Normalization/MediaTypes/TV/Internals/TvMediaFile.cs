namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvMediaFile
{
    public TvMediaFile(string tvRootPath, string filePath, bool isFromDestinationScan = false)
    {
        TvRootPath = tvRootPath;
        FilePath = filePath;
        IsFromDestinationScan = isFromDestinationScan;
    }

    public string TvRootPath { get; }

    public string FilePath { get; }

    public bool IsFromDestinationScan { get; }
}
