using System.Net;
using System.Text;
using Application.Abstractions.Imdb.Models;
using Application.Configuration;
using Infrastructure.Imdb;
using Xunit;

namespace Application.Tests.Infrastructure.Imdb;

public sealed class ImdbClientTests
{
    [Fact]
    public async Task GetEpisodeAsync_SendsSeriesSeasonAndEpisodeParameters()
    {
        var handler = new StubHttpMessageHandler(
            """
            {"Title":"Hands","Year":"2022","imdbID":"tt2000001","Type":"episode","seriesID":"tt14452776","Season":"1","Episode":"2","Response":"True"}
            """);
        var client = CreateClient(handler);

        var result = await client.GetEpisodeAsync("tt14452776", 1, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hands", result.Value?.Title);
        var query = handler.RequestUri?.Query;
        Assert.Contains("i=tt14452776", query, StringComparison.Ordinal);
        Assert.Contains("Season=1", query, StringComparison.Ordinal);
        Assert.Contains("Episode=2", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEpisodeAsync_ReturnsInvalidResponseForWrongEpisode()
    {
        var handler = new StubHttpMessageHandler(
            """
            {"Title":"Hands","Year":"2022","imdbID":"tt2000001","Type":"episode","seriesID":"tt14452776","Season":"1","Episode":"3","Response":"True"}
            """);
        var client = CreateClient(handler);

        var result = await client.GetEpisodeAsync("tt14452776", 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImdbErrorKind.InvalidResponse, result.Error?.Kind);
    }

    private static ImdbClient CreateClient(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new OmdbClientSettings("https://example.test/", "test-key", 30));

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string responseContent;

        public StubHttpMessageHandler(string responseContent) =>
            this.responseContent = responseContent;

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });
        }
    }
}
