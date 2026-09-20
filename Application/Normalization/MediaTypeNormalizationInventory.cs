using Application.Abstractions.Database.Models;

namespace Application.Normalization;

public sealed class MediaTypeNormalizationInventory
{
    public MediaTypeNormalizationInventory(
        IEnumerable<MediaFile> files,
        IEnumerable<MediaTitle> titles)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(titles);

        Files = files.ToArray();
        Titles = titles.ToArray();
    }

    public MediaFile[] Files { get; }

    public MediaTitle[] Titles { get; }
}
