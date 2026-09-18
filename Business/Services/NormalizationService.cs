using Application.Normalization;

namespace Business.Services;

public sealed class NormalizationService(
    MediaLibraryNormalizationRequest mediaLibrary,
    MediaTypeHandler mediaTypeHandler) : INormalizationService
{
    public async Task NormalizeMediaFiles()
    {
        foreach (var mediaType in mediaLibrary.MediaTypes)
        {
            await mediaTypeHandler.Process(mediaType, mediaLibrary.Locations);
        }
    }
}
