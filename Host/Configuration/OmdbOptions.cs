namespace Host.Configuration;

public sealed class OmdbOptions
{
    public const string SectionName = "Omdb";

    public string BaseUrl { get; init; } = "https://www.omdbapi.com/";

    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 10;
}
