using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;

namespace Infrastructure.Database;

public sealed class NormalizationDeletedDirectoriesRepository(IDbConnectionFactory connectionFactory)
    : SqlRepositoryBase(connectionFactory), INormalizationDeletedDirectoriesRepository
{
    public Task<NormalizationDeletedDirectory> UpsertAsync(
        NormalizationDeletedDirectoryUpsert request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteSingleAsync(
            "dbo.NormalizationDeletedDirectories_Upsert",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, request.Id);
                AddParameter(command, "@NormalizationRunId", DbType.Int64, request.NormalizationRunId);
                AddParameter(command, "@MediaTypeId", DbType.Int32, request.MediaTypeId);
                AddParameter(command, "@Path", DbType.String, request.Path, 2048);
            },
            Map,
            cancellationToken);
    }

    public Task<NormalizationDeletedDirectory> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSingleAsync(
            "dbo.NormalizationDeletedDirectories_Delete",
            command => AddParameter(command, "@Id", DbType.Int64, id),
            Map,
            cancellationToken);
    }

    private static NormalizationDeletedDirectory Map(DbDataReader reader)
    {
        return new NormalizationDeletedDirectory(
            GetValue<long>(reader, "Id"),
            GetValue<long>(reader, "NormalizationRunId"),
            GetValue<int>(reader, "MediaTypeId"),
            GetValue<string>(reader, "Path"),
            GetValue<DateTime>(reader, "DeletedAtUtc"));
    }
}
