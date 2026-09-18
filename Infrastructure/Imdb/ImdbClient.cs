using System.Globalization;
using System.Text.Json;
using Application.Abstractions.Imdb;
using Application.Abstractions.Imdb.Models;
using Application.Configuration;
using Infrastructure.Imdb.Internal;

namespace Infrastructure.Imdb;

public sealed class ImdbClient : IImdbClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public ImdbClient(HttpClient httpClient, OmdbClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ApiKey);

        this.httpClient = httpClient;
        apiKey = settings.ApiKey;
    }

    public async Task<ImdbResult<ImdbSearchPage>> SearchAsync(
        string title,
        int? year = null,
        ImdbTitleType? type = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (year is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be positive.");
        }

        if (page is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be between 1 and 100.");
        }

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("apikey", apiKey),
            new("s", title),
            new("page", page.ToString(CultureInfo.InvariantCulture)),
            new("r", "json")
        };

        if (year.HasValue)
        {
            parameters.Add(new("y", year.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (type.HasValue)
        {
            parameters.Add(new("type", ToQueryValue(type.Value)));
        }

        var response = await GetAsync<OmdbSearchResponseDto>(BuildQuery(parameters), cancellationToken);

        if (response.Error is not null)
        {
            return ImdbResult<ImdbSearchPage>.Failure(response.Error);
        }

        var payload = response.Value!;
        if (!IsSuccessful(payload.Response))
        {
            return ImdbResult<ImdbSearchPage>.Failure(CreateApiError(payload.Error));
        }

        if (payload.Search is null
            || !int.TryParse(payload.TotalResults, NumberStyles.None, CultureInfo.InvariantCulture, out var totalResults))
        {
            return InvalidResponse<ImdbSearchPage>();
        }

        var results = new List<ImdbTitleSummary>(payload.Search.Length);
        foreach (var item in payload.Search)
        {
            if (string.IsNullOrWhiteSpace(item.ImdbId)
                || string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.Year)
                || !TryParseTitleType(item.Type, out var itemType))
            {
                return InvalidResponse<ImdbSearchPage>();
            }

            results.Add(new ImdbTitleSummary(item.ImdbId, item.Title, item.Year, itemType));
        }

        return ImdbResult<ImdbSearchPage>.Success(new ImdbSearchPage(results, totalResults, page));
    }

    public async Task<ImdbResult<ImdbTitleDetails>> GetByIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);

        if (!imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("An IMDb title ID must begin with 'tt'.", nameof(imdbId));
        }

        var response = await GetAsync<OmdbTitleResponseDto>(
            BuildQuery(
            [
                new("apikey", apiKey),
                new("i", imdbId),
                new("r", "json")
            ]),
            cancellationToken);

        if (response.Error is not null)
        {
            return ImdbResult<ImdbTitleDetails>.Failure(response.Error);
        }

        var payload = response.Value!;
        if (!IsSuccessful(payload.Response))
        {
            return ImdbResult<ImdbTitleDetails>.Failure(CreateApiError(payload.Error));
        }

        if (string.IsNullOrWhiteSpace(payload.ImdbId)
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.Year)
            || !TryParseTitleType(payload.Type, out var titleType))
        {
            return InvalidResponse<ImdbTitleDetails>();
        }

        return ImdbResult<ImdbTitleDetails>.Success(
            new ImdbTitleDetails(
                payload.ImdbId,
                payload.Title,
                payload.Year,
                titleType,
                NullIfUnavailable(payload.SeriesImdbId),
                ParseOptionalNumber(payload.Season),
                ParseOptionalNumber(payload.Episode)));
    }

    public async Task<ImdbResult<ImdbTitleDetails>> GetEpisodeAsync(
        string seriesImdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesImdbId);

        if (!seriesImdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("An IMDb title ID must begin with 'tt'.", nameof(seriesImdbId));
        }

        if (seasonNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonNumber), "Season number must be positive.");
        }

        if (episodeNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeNumber), "Episode number must be positive.");
        }

        var response = await GetAsync<OmdbTitleResponseDto>(
            BuildQuery(
            [
                new("apikey", apiKey),
                new("i", seriesImdbId),
                new("Season", seasonNumber.ToString(CultureInfo.InvariantCulture)),
                new("Episode", episodeNumber.ToString(CultureInfo.InvariantCulture)),
                new("r", "json")
            ]),
            cancellationToken);

        if (response.Error is not null)
        {
            return ImdbResult<ImdbTitleDetails>.Failure(response.Error);
        }

        var payload = response.Value!;
        if (!IsSuccessful(payload.Response))
        {
            return ImdbResult<ImdbTitleDetails>.Failure(CreateApiError(payload.Error));
        }

        if (string.IsNullOrWhiteSpace(payload.ImdbId)
            || string.IsNullOrWhiteSpace(payload.Title)
            || string.IsNullOrWhiteSpace(payload.Year)
            || !TryParseTitleType(payload.Type, out var titleType)
            || titleType != ImdbTitleType.Episode
            || !string.Equals(payload.SeriesImdbId, seriesImdbId, StringComparison.OrdinalIgnoreCase)
            || ParseOptionalNumber(payload.Season) != seasonNumber
            || ParseOptionalNumber(payload.Episode) != episodeNumber)
        {
            return InvalidResponse<ImdbTitleDetails>();
        }

        return ImdbResult<ImdbTitleDetails>.Success(
            new ImdbTitleDetails(
                payload.ImdbId,
                payload.Title,
                payload.Year,
                titleType,
                payload.SeriesImdbId,
                seasonNumber,
                episodeNumber));
    }

    private async Task<ClientResponse<T>> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ClientResponse<T>.Failure(new ImdbError(
                    ImdbErrorKind.Http,
                    $"OMDb returned HTTP status {(int)response.StatusCode}.",
                    response.StatusCode));
            }

            try
            {
                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<T>(content, SerializerOptions, cancellationToken);

                return payload is null
                    ? ClientResponse<T>.Failure(CreateInvalidResponseError())
                    : ClientResponse<T>.Success(payload);
            }
            catch (JsonException)
            {
                return ClientResponse<T>.Failure(CreateInvalidResponseError());
            }
            catch (NotSupportedException)
            {
                return ClientResponse<T>.Failure(CreateInvalidResponseError());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ClientResponse<T>.Failure(new ImdbError(ImdbErrorKind.Timeout, "The OMDb request timed out."));
        }
        catch (HttpRequestException)
        {
            return ClientResponse<T>.Failure(new ImdbError(
                ImdbErrorKind.Transport,
                "The OMDb request could not be completed."));
        }
    }

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> parameters) =>
        $"?{string.Join('&', parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"))}";

    private static string ToQueryValue(ImdbTitleType type) => type switch
    {
        ImdbTitleType.Movie => "movie",
        ImdbTitleType.Series => "series",
        ImdbTitleType.Episode => "episode",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported IMDb title type.")
    };

    private static bool TryParseTitleType(string? value, out ImdbTitleType type)
    {
        switch (value?.ToLowerInvariant())
        {
            case "movie": type = ImdbTitleType.Movie; return true;
            case "series": type = ImdbTitleType.Series; return true;
            case "episode": type = ImdbTitleType.Episode; return true;
            default: type = default; return false;
        }
    }

    private static bool IsSuccessful(string? response) =>
        string.Equals(response, "True", StringComparison.OrdinalIgnoreCase);

    private static ImdbError CreateApiError(string? message)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message)
            ? "OMDb returned an unspecified error."
            : message;

        var kind = errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? ImdbErrorKind.NotFound
            : errorMessage.Contains("api key", StringComparison.OrdinalIgnoreCase)
                ? ImdbErrorKind.Authentication
                : errorMessage.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    ? ImdbErrorKind.RateLimit
                    : ImdbErrorKind.Api;

        return new ImdbError(kind, errorMessage);
    }

    private static string? NullIfUnavailable(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;

    private static int? ParseOptionalNumber(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static ImdbResult<T> InvalidResponse<T>() =>
        ImdbResult<T>.Failure(CreateInvalidResponseError());

    private static ImdbError CreateInvalidResponseError() =>
        new(ImdbErrorKind.InvalidResponse, "OMDb returned an invalid response.");

    private sealed class ClientResponse<T>
    {
        private ClientResponse(T? value, ImdbError? error)
        {
            Value = value;
            Error = error;
        }

        public T? Value { get; }

        public ImdbError? Error { get; }

        public static ClientResponse<T> Success(T value) => new(value, null);

        public static ClientResponse<T> Failure(ImdbError error) => new(default, error);
    }
}
