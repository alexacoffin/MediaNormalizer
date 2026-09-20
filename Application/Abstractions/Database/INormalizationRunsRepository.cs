using Application.Abstractions.Database.Models;

namespace Application.Abstractions.Database;

public interface INormalizationRunsRepository
{
    Task<NormalizationRun> UpsertAsync(
        NormalizationRunUpsert request,
        CancellationToken cancellationToken = default);

    Task<NormalizationRun> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}
