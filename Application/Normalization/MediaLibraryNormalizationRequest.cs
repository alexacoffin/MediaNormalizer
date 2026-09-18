using Domain.Enums;

namespace Application.Normalization;

public sealed class MediaLibraryNormalizationRequest
{
    public MediaLibraryNormalizationRequest(
        string[] locations,
        MediaTypeNormalizationRequest[] mediaTypes)
    {
        Locations = locations;
        MediaTypes = mediaTypes;
    }

    public string[] Locations { get; }

    public MediaTypeNormalizationRequest[] MediaTypes { get; }
}

public sealed class MediaTypeNormalizationRequest
{
    public MediaTypeNormalizationRequest(
        MediaType id,
        string name,
        string subdirectory,
        string outputDirectory,
        bool enabled)
    {
        Id = id;
        Name = name;
        Subdirectory = subdirectory;
        OutputDirectory = outputDirectory;
        Enabled = enabled;
    }

    public MediaType Id { get; }

    public string Name { get; }

    public string Subdirectory { get; }

    public string OutputDirectory { get; }

    public bool Enabled { get; }
}
