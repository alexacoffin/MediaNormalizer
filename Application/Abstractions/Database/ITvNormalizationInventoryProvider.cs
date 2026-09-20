using Application.Normalization;

namespace Application.Abstractions.Database;

public interface ITvNormalizationInventoryProvider
{
    Task<MediaTypeNormalizationInventory?> GetAsync(
        CancellationToken cancellationToken = default);
}
