using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;

namespace Infrastructure.Database;

public sealed class MediaTitlesRepository(IDbConnectionFactory connectionFactory)
    : SqlRepositoryBase(connectionFactory), IMediaTitlesRepository
{
    public Task<MediaTitle> UpsertAsync(
        MediaTitleUpsert request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteSingleAsync(
            "dbo.MediaTitles_Upsert",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, request.Id);
                AddParameter(command, "@MediaTypeId", DbType.Int32, request.MediaTypeId);
                AddParameter(command, "@OmdbEntryId", DbType.AnsiString, request.OmdbEntryId, 32);
                AddParameter(command, "@Name", DbType.String, request.Name, 512);
                AddParameter(command, "@ReleaseYear", DbType.Int16, request.ReleaseYear);
                AddParameter(command, "@LastSeenRunId", DbType.Int64, request.LastSeenRunId);
                AddParameter(command, "@IsActive", DbType.Boolean, request.IsActive);
            },
            Map,
            cancellationToken);
    }

    public Task<MediaTitle> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSingleAsync(
            "dbo.MediaTitles_Delete",
            command => AddParameter(command, "@Id", DbType.Int64, id),
            Map,
            cancellationToken);
    }

    public Task<MediaTitle?> GetByIdAsync(
        long id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return QuerySingleOrDefaultAsync(
            "dbo.MediaTitles_GetById",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, id);
                AddParameter(command, "@IncludeInactive", DbType.Boolean, includeInactive);
            },
            Map,
            cancellationToken);
    }

    public Task<MediaTitle?> GetByOmdbEntryIdAsync(
        int mediaTypeId,
        string omdbEntryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omdbEntryId);

        return QuerySingleOrDefaultAsync(
            "dbo.MediaTitles_GetByOmdbEntryId",
            command =>
            {
                AddParameter(command, "@MediaTypeId", DbType.Int32, mediaTypeId);
                AddParameter(command, "@OmdbEntryId", DbType.AnsiString, omdbEntryId, 32);
                AddParameter(command, "@IncludeInactive", DbType.Boolean, includeInactive);
            },
            Map,
            cancellationToken);
    }

    public Task<IReadOnlyList<MediaTitle>> GetActiveByMediaTypeAsync(
        int mediaTypeId,
        CancellationToken cancellationToken = default)
    {
        return QueryManyAsync(
            "dbo.MediaTitles_GetActiveByMediaType",
            command => AddParameter(command, "@MediaTypeId", DbType.Int32, mediaTypeId),
            Map,
            cancellationToken);
    }

    private static MediaTitle Map(DbDataReader reader)
    {
        return new MediaTitle(
            GetValue<long>(reader, "Id"),
            GetValue<int>(reader, "MediaTypeId"),
            GetNullableString(reader, "OmdbEntryId"),
            GetNullableString(reader, "Name"),
            GetNullableValue<short>(reader, "ReleaseYear"),
            GetValue<DateTime>(reader, "DateCreated"),
            GetValue<DateTime>(reader, "LastModified"),
            GetNullableValue<long>(reader, "LastSeenRunId"),
            GetValue<bool>(reader, "IsActive"));
    }
}
