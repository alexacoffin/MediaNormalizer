using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;

namespace Infrastructure.Database;

public sealed class MediaFilesRepository(IDbConnectionFactory connectionFactory)
    : SqlRepositoryBase(connectionFactory), IMediaFilesRepository
{
    public Task<MediaFile> UpsertAsync(
        MediaFileUpsert request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteSingleAsync(
            "dbo.MediaFiles_Upsert",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, request.Id);
                AddParameter(command, "@TitleId", DbType.Int64, request.TitleId);
                AddParameter(command, "@MediaTypeId", DbType.Int32, request.MediaTypeId);
                AddParameter(command, "@CurrentPath", DbType.String, request.CurrentPath, 2048);
                AddParameter(command, "@CanonicalPath", DbType.String, request.CanonicalPath, 2048);
                AddParameter(command, "@SeasonNumber", DbType.Int32, request.SeasonNumber);
                AddParameter(command, "@EpisodeNumber", DbType.Int32, request.EpisodeNumber);
                AddParameter(
                    command,
                    "@AirDate",
                    DbType.Date,
                    request.AirDate?.ToDateTime(TimeOnly.MinValue));
                AddParameter(command, "@EpisodeTitle", DbType.String, request.EpisodeTitle, 512);
                AddParameter(command, "@LastStatus", DbType.AnsiString, request.LastStatus, 32);
                AddParameter(command, "@LastMessage", DbType.String, request.LastMessage, 4000);
                AddParameter(command, "@FirstSeenRunId", DbType.Int64, request.FirstSeenRunId);
                AddParameter(command, "@LastSeenRunId", DbType.Int64, request.LastSeenRunId);
                AddParameter(command, "@IsActive", DbType.Boolean, request.IsActive);
            },
            Map,
            cancellationToken);
    }

    public Task<MediaFile> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSingleAsync(
            "dbo.MediaFiles_Delete",
            command => AddParameter(command, "@Id", DbType.Int64, id),
            Map,
            cancellationToken);
    }

    public Task<MediaFile?> GetByIdAsync(
        long id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return QuerySingleOrDefaultAsync(
            "dbo.MediaFiles_GetById",
            command => ConfigureReadById(command, id, includeInactive),
            Map,
            cancellationToken);
    }

    public Task<IReadOnlyList<MediaFile>> GetByTitleIdAsync(
        long titleId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return QueryManyAsync(
            "dbo.MediaFiles_GetByTitleId",
            command =>
            {
                AddParameter(command, "@TitleId", DbType.Int64, titleId);
                AddParameter(command, "@IncludeInactive", DbType.Boolean, includeInactive);
            },
            Map,
            cancellationToken);
    }

    public Task<MediaFile?> GetByCurrentPathAsync(
        string currentPath,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);

        return QuerySingleOrDefaultAsync(
            "dbo.MediaFiles_GetByCurrentPath",
            command =>
            {
                AddParameter(command, "@CurrentPath", DbType.String, currentPath, 2048);
                AddParameter(command, "@IncludeInactive", DbType.Boolean, includeInactive);
            },
            Map,
            cancellationToken);
    }

    public Task<IReadOnlyList<MediaFile>> GetActiveByMediaTypeAsync(
        int mediaTypeId,
        CancellationToken cancellationToken = default)
    {
        return QueryManyAsync(
            "dbo.MediaFiles_GetActiveByMediaType",
            command => AddParameter(command, "@MediaTypeId", DbType.Int32, mediaTypeId),
            Map,
            cancellationToken);
    }

    private static void ConfigureReadById(DbCommand command, long id, bool includeInactive)
    {
        AddParameter(command, "@Id", DbType.Int64, id);
        AddParameter(command, "@IncludeInactive", DbType.Boolean, includeInactive);
    }

    private static MediaFile Map(DbDataReader reader)
    {
        return new MediaFile(
            GetValue<long>(reader, "Id"),
            GetNullableValue<long>(reader, "TitleId"),
            GetValue<int>(reader, "MediaTypeId"),
            GetValue<string>(reader, "CurrentPath"),
            GetNullableString(reader, "CanonicalPath"),
            GetNullableValue<int>(reader, "SeasonNumber"),
            GetNullableValue<int>(reader, "EpisodeNumber"),
            GetNullableDateOnly(reader, "AirDate"),
            GetNullableString(reader, "EpisodeTitle"),
            GetNullableString(reader, "LastStatus"),
            GetNullableString(reader, "LastMessage"),
            GetNullableValue<long>(reader, "FirstSeenRunId"),
            GetNullableValue<long>(reader, "LastSeenRunId"),
            GetValue<DateTime>(reader, "DateCreated"),
            GetValue<DateTime>(reader, "LastModified"),
            GetValue<bool>(reader, "IsActive"));
    }
}
