using System.Text.Json.Serialization;

namespace Infrastructure.Imdb.Internal;

internal sealed class OmdbSearchItemDto
{
    [JsonPropertyName("Title")]
    public string? Title { get; init; }

    [JsonPropertyName("Year")]
    public string? Year { get; init; }

    [JsonPropertyName("imdbID")]
    public string? ImdbId { get; init; }

    [JsonPropertyName("Type")]
    public string? Type { get; init; }
}
