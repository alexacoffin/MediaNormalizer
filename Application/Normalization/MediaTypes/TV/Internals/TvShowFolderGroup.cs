namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvShowFolderGroup
{
    public TvShowFolderGroup(
        string tvRootPath,
        string showFolderPath,
        string showFolderName,
        string[] filePaths)
    {
        TvRootPath = tvRootPath;
        ShowFolderPath = showFolderPath;
        ShowFolderName = showFolderName;
        FilePaths = filePaths;
    }

    public string TvRootPath { get; }

    public string ShowFolderPath { get; }

    public string ShowFolderName { get; }

    public string[] FilePaths { get; }
}
