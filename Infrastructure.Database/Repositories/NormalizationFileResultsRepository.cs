using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;

namespace Infrastructure.Database;

public sealed class NormalizationFileResultsRepository(IDbConnectionFactory connectionFactory)
    : SqlRepositoryBase(connectionFactory), INormalizationFileResultsRepository
{
    public Task<NormalizationFileResult> UpsertAsync(
        NormalizationFileResultUpsert request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExecuteSingleAsync(
            "dbo.NormalizationFileResults_Upsert",
            command =>
            {
                AddParameter(command, "@Id", DbType.Int64, request.Id);
                AddParameter(command, "@NormalizationRunId", DbType.Int64, request.NormalizationRunId);
                AddParameter(command, "@MediaFileId", DbType.Int64, request.MediaFileId);
                AddParameter(command, "@MediaTitleId", DbType.Int64, request.MediaTitleId);
                AddParameter(command, "@MediaTypeId", DbType.Int32, request.MediaTypeId);
                AddParameter(command, "@SourcePath", DbType.String, request.SourcePath, 2048);
                AddParameter(command, "@DestinationPath", DbType.String, request.DestinationPath, 2048);
                AddParameter(command, "@Status", DbType.AnsiString, request.Status, 32);
                AddParameter(command, "@Message", DbType.String, request.Message, 4000);
                AddParameter(command, "@SourceRole", DbType.AnsiString, request.SourceRole, 20);
            },
            Map,
            cancellationToken);
    }

    public Task<NormalizationFileResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteSingleAsync(
            "dbo.NormalizationFileResults_Delete",
            command => AddParameter(command, "@Id", DbType.Int64, id),
            Map,
            cancellationToken);
    }

    private static NormalizationFileResult Map(DbDataReader reader)
    {
        return new NormalizationFileResult(
            GetValue<long>(reader, "Id"),
            GetValue<long>(reader, "NormalizationRunId"),
            GetNullableValue<long>(reader, "MediaFileId"),
            GetNullableValue<long>(reader, "MediaTitleId"),
            GetValue<int>(reader, "MediaTypeId"),
            GetValue<string>(reader, "SourcePath"),
            GetNullableString(reader, "DestinationPath"),
            GetValue<string>(reader, "Status"),
            GetValue<string>(reader, "Message"),
            GetValue<string>(reader, "SourceRole"),
            GetValue<DateTime>(reader, "CreatedAtUtc"));
    }
}
