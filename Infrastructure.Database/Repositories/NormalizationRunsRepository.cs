using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;

namespace Infrastructure.Database;

public sealed class NormalizationRunsRepository(IDbConnectionFactory connectionFactory)
    : SqlRepositoryBase(connectionFactory), INormalizationRunsRepository
{
    public Task<NormalizationRun> UpsertAsync(
        NormalizationRunUpsert request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteSingleAsync(
            "dbo.NormalizationRuns_Upsert",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, request.Id);
                AddParameter(command, "@CompletedAtUtc", DbType.DateTime2, request.CompletedAtUtc);
                AddParameter(command, "@Status", DbType.AnsiString, request.Status, 20);
                AddParameter(command, "@ErrorMessage", DbType.String, request.ErrorMessage, 4000);
                AddParameter(command, "@StartedAtUtc", DbType.DateTime2, request.StartedAtUtc);
            },
            Map,
            cancellationToken);
    }

    public Task<NormalizationRun> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSingleAsync(
            "dbo.NormalizationRuns_Delete",
            command => AddParameter(command, "@Id", DbType.Int64, id),
            Map,
            cancellationToken);
    }

    private static NormalizationRun Map(DbDataReader reader)
    {
        return new NormalizationRun(
            GetValue<long>(reader, "Id"),
            GetValue<DateTime>(reader, "StartedAtUtc"),
            GetNullableValue<DateTime>(reader, "CompletedAtUtc"),
            GetValue<string>(reader, "Status"),
            GetNullableString(reader, "ErrorMessage"));
    }
}
