using Application.Abstractions.Database.Models;

namespace Application.Abstractions.Database;

public interface IMediaTitlesRepository
{
    Task<MediaTitle> UpsertAsync(
        MediaTitleUpsert request,
        CancellationToken cancellationToken = default);

    Task<MediaTitle> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<MediaTitle?> GetByIdAsync(
        long id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<MediaTitle?> GetByOmdbEntryIdAsync(
        int mediaTypeId,
        string omdbEntryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaTitle>> GetActiveByMediaTypeAsync(
        int mediaTypeId,
        CancellationToken cancellationToken = default);
}
