using Application.Abstractions.FileSystem;
using Application.Abstractions.Imdb;
using Application.Normalization.MediaTypes.Movies;
using Application.Normalization.MediaTypes.TV;
using Domain.Enums;

namespace Application.Normalization;

public sealed class MediaTypeHandler(
    IFileManager fileManager,
    IImdbClient imdbClient)
{
    public async Task<MediaTypeNormalizationResult> Process(
        MediaTypeNormalizationRequest mediaType,
        string[] locations,
        CancellationToken cancellationToken = default)
    {
        if (!mediaType.Enabled)
        {
            return MediaTypeNormalizationResult.Empty;
        }

        IMediaTypeHandler? handler = mediaType.Id switch
        {
            MediaType.Tv => new TvMediaTypeHandler(
                locations,
                mediaType.OutputDirectory,
                fileManager,
                imdbClient),
            MediaType.Movies => new MovieMediaTypeHandler(locations),
            _ => null
        };

        return handler is null
            ? MediaTypeNormalizationResult.Empty
            : await handler.Normalize(cancellationToken);
    }
}
