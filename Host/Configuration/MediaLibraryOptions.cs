namespace Host.Configuration;

public sealed class MediaLibraryOptions
{
    public string[] Locations { get; init; } = [];

    public MediaTypeOptions[] MediaTypes { get; init; } = [];
}
