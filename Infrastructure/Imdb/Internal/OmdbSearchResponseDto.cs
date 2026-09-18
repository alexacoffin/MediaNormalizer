using System.Text.Json.Serialization;

namespace Infrastructure.Imdb.Internal;

internal sealed class OmdbSearchResponseDto
{
    [JsonPropertyName("Search")]
    public OmdbSearchItemDto[]? Search { get; init; }

    [JsonPropertyName("totalResults")]
    public string? TotalResults { get; init; }

    [JsonPropertyName("Response")]
    public string? Response { get; init; }

    [JsonPropertyName("Error")]
    public string? Error { get; init; }
}
