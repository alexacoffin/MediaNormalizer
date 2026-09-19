using Application.Normalization;

namespace Business.Services;

public interface INormalizationService
{
    Task<NormalizationResult> NormalizeMediaFiles();
}
