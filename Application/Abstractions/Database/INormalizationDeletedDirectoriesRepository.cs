using Application.Abstractions.Database.Models;

namespace Application.Abstractions.Database;

public interface INormalizationDeletedDirectoriesRepository
{
    Task<NormalizationDeletedDirectory> UpsertAsync(
        NormalizationDeletedDirectoryUpsert request,
        CancellationToken cancellationToken = default);

    Task<NormalizationDeletedDirectory> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}
