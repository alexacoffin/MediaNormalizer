using Application.Normalization;

namespace Business.Services;

public sealed class NormalizationService(
    MediaLibraryNormalizationRequest mediaLibrary,
    MediaTypeHandler mediaTypeHandler) : INormalizationService
{
    public async Task<NormalizationResult> NormalizeMediaFiles()
    {
        var fileResults = new List<MediaFileNormalizationResult>();
        var deletedDirectories = new List<string>();

        foreach (var mediaType in mediaLibrary.MediaTypes)
        {
            var result = await mediaTypeHandler.Process(mediaType, mediaLibrary.Locations);
            fileResults.AddRange(result.FileResults);
            deletedDirectories.AddRange(result.DeletedDirectories);
        }

        return new NormalizationResult(fileResults, deletedDirectories);
    }
}
