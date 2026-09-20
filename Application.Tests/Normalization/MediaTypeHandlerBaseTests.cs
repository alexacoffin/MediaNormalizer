using Application.Normalization;
using Application.Normalization.MediaTypes.Movies;
using Xunit;

namespace Application.Tests.Normalization;

public sealed class MediaTypeHandlerBaseTests
{
    [Fact]
    public async Task MovieHandlerUsesBaseNoOpPersistenceAndReconciliation()
    {
        var handler = new MovieMediaTypeHandler([]);
        var mediaType = new MediaTypeNormalizationRequest(
            Domain.Enums.MediaType.Movies,
            "Movies",
            "Movies",
            Path.Combine(Path.GetTempPath(), "Movies"),
            true);

        Assert.IsAssignableFrom<MediaTypeHandlerBase>(handler);
        var result = await handler.ProcessAsync(mediaType, 42);

        Assert.False(result.ProcessedSuccessfully);
        Assert.Null(result.PersistenceFailure);
    }
}
