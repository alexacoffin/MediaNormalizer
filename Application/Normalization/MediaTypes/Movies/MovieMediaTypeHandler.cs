using Application.Normalization;

namespace Application.Normalization.MediaTypes.Movies;

public sealed class MovieMediaTypeHandler : IMediaTypeHandler
{
    public MovieMediaTypeHandler(string[] locations)
    {
    }

    public Task<MediaTypeNormalizationResult> Normalize() =>
        Task.FromResult(MediaTypeNormalizationResult.Empty);
}
