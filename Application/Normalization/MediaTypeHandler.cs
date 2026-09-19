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
    public async Task Process(
        MediaTypeNormalizationRequest mediaType,
        string[] locations)
    {
        if (!mediaType.Enabled)
        {
            return;
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

        if (handler is not null)
        {
            await handler.Normalize();
        }
    }
}
