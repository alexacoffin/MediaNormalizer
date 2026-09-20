using Application.Normalization;

namespace Application.Normalization.MediaTypes.Movies;

public sealed class MovieMediaTypeHandler : IMediaTypeHandler
{
    public MovieMediaTypeHandler(string[] locations)
    {
    }

    public Task<MediaTypeNormalizationResult> Normalize(CancellationToken cancellationToken = default) =>
        Task.FromResult(MediaTypeNormalizationResult.Empty);
}
