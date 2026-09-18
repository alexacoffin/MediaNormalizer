using System.Text.Json.Serialization;

namespace Infrastructure.Imdb.Internal;

internal sealed class OmdbTitleResponseDto
{
    [JsonPropertyName("Title")]
    public string? Title { get; init; }

    [JsonPropertyName("Year")]
    public string? Year { get; init; }

    [JsonPropertyName("imdbID")]
    public string? ImdbId { get; init; }

    [JsonPropertyName("Type")]
    public string? Type { get; init; }

    [JsonPropertyName("seriesID")]
    public string? SeriesImdbId { get; init; }

    [JsonPropertyName("Season")]
    public string? Season { get; init; }

    [JsonPropertyName("Episode")]
    public string? Episode { get; init; }

    [JsonPropertyName("Response")]
    public string? Response { get; init; }

    [JsonPropertyName("Error")]
    public string? Error { get; init; }
}
