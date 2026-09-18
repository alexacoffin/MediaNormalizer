namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvShowFolderGroupingResult
{
    public TvShowFolderGroupingResult(
        TvShowFolderGroup[] showFolderGroups,
        TvUnsupportedFileGroup[] unsupportedFileGroups)
    {
        ShowFolderGroups = showFolderGroups;
        UnsupportedFileGroups = unsupportedFileGroups;
    }

    public TvShowFolderGroup[] ShowFolderGroups { get; }

    public TvUnsupportedFileGroup[] UnsupportedFileGroups { get; }
}
