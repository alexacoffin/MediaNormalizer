namespace Application.Normalization;

public interface IMediaTypeHandler
{
    Task<MediaTypeNormalizationResult> Normalize(CancellationToken cancellationToken = default);
}
