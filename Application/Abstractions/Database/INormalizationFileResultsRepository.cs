using Application.Abstractions.Database.Models;

namespace Application.Abstractions.Database;

public interface INormalizationFileResultsRepository
{
    Task<NormalizationFileResult> UpsertAsync(
        NormalizationFileResultUpsert request,
        CancellationToken cancellationToken = default);

    Task<NormalizationFileResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}
