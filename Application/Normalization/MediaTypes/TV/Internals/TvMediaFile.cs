namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvMediaFile
{
    public TvMediaFile(string tvRootPath, string filePath)
    {
        TvRootPath = tvRootPath;
        FilePath = filePath;
    }

    public string TvRootPath { get; }

    public string FilePath { get; }
}
