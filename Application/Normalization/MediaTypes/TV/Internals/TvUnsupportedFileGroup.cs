namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvUnsupportedFileGroup
{
    public TvUnsupportedFileGroup(string tvRootPath, string[] filePaths)
    {
        TvRootPath = tvRootPath;
        FilePaths = filePaths;
    }

    public string TvRootPath { get; }

    public string[] FilePaths { get; }
}
