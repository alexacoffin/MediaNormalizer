using Application.Abstractions.Database.Models;

namespace Application.Abstractions.Database;

public interface IMediaFilesRepository
{
    Task<MediaFile> UpsertAsync(
        MediaFileUpsert request,
        CancellationToken cancellationToken = default);

    Task<MediaFile> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<MediaFile?> GetByIdAsync(
        long id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFile>> GetByTitleIdAsync(
        long titleId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<MediaFile?> GetByCurrentPathAsync(
        string currentPath,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFile>> GetActiveByMediaTypeAsync(
        int mediaTypeId,
        CancellationToken cancellationToken = default);
}
