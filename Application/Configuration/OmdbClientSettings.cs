namespace Application.Configuration;

public sealed class OmdbClientSettings
{
    public OmdbClientSettings(string baseUrl, string apiKey, int timeoutSeconds)
    {
        BaseUrl = baseUrl;
        ApiKey = apiKey;
        TimeoutSeconds = timeoutSeconds;
    }

    public string BaseUrl { get; }

    public string ApiKey { get; }

    public int TimeoutSeconds { get; }
}
