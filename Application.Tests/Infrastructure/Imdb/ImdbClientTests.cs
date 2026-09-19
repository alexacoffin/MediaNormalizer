using System.Net;
using System.Text;
using Application.Abstractions.Imdb.Models;
using Application.Configuration;
using Infrastructure.Imdb;
using Moq;
using Moq.Protected;
using Xunit;

namespace Application.Tests.Infrastructure.Imdb;

public sealed class ImdbClientTests
{
    [Fact]
    public async Task GetEpisodeAsync_SendsSeriesSeasonAndEpisodeParameters()
    {
        var handler = CreateHandler(
            """
            {"Title":"Hands","Year":"2022","imdbID":"tt2000001","Type":"episode","seriesID":"tt14452776","Season":"1","Episode":"2","Response":"True"}
            """);
        var client = CreateClient(handler.Mock);

        var result = await client.GetEpisodeAsync("tt14452776", 1, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hands", result.Value?.Title);
        var query = handler.RequestUri?.Query;
        Assert.Contains("i=tt14452776", query, StringComparison.Ordinal);
        Assert.Contains("Season=1", query, StringComparison.Ordinal);
        Assert.Contains("Episode=2", query, StringComparison.Ordinal);
        handler.Mock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetEpisodeAsync_ReturnsInvalidResponseForWrongEpisode()
    {
        var handler = CreateHandler(
            """
            {"Title":"Hands","Year":"2022","imdbID":"tt2000001","Type":"episode","seriesID":"tt14452776","Season":"1","Episode":"3","Response":"True"}
            """);
        var client = CreateClient(handler.Mock);

        var result = await client.GetEpisodeAsync("tt14452776", 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImdbErrorKind.InvalidResponse, result.Error?.Kind);
    }

    private static HandlerFixture CreateHandler(string responseContent)
    {
        var fixture = new HandlerFixture(responseContent);
        fixture.Mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(
                (request, _) => fixture.RequestUri = request.RequestUri)
            .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            }));

        return fixture;
    }

    private static ImdbClient CreateClient(Mock<HttpMessageHandler> handler) =>
        new(
            new HttpClient(handler.Object) { BaseAddress = new Uri("https://example.test/") },
            new OmdbClientSettings("https://example.test/", "test-key", 30));

    private sealed class HandlerFixture
    {
        public HandlerFixture(string responseContent)
        {
            Mock = new Mock<HttpMessageHandler>();
        }

        public Mock<HttpMessageHandler> Mock { get; }

        public Uri? RequestUri { get; set; }
    }
}
