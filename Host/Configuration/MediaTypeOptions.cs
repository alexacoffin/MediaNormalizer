using Domain.Enums;

namespace Host.Configuration;

public sealed class MediaTypeOptions
{
    public MediaType Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Subdirectory { get; init; } = string.Empty;

    public string OutputDirectory { get; init; } = string.Empty;

    public bool Enabled { get; init; }
}
