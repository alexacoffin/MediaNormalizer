using System.Data;
using System.Data.Common;
using Application.Abstractions.Database;

namespace Infrastructure.Database;

public abstract class SqlRepositoryBase(IDbConnectionFactory connectionFactory)
{
    protected IDbConnectionFactory ConnectionFactory { get; } = connectionFactory;

    protected async Task<T> ExecuteSingleAsync<T>(
        string procedureName,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        configureCommand(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Stored procedure '{procedureName}' did not return a row.");
        }

        return map(reader);
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
        where T : class
    {
        await using var connection = ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        configureCommand(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? map(reader) : null;
    }

    protected async Task<IReadOnlyList<T>> QueryManyAsync<T>(
        string procedureName,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = ConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        configureCommand(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        return results;
    }

    protected static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    protected static T GetValue<T>(DbDataReader reader, string columnName)
    {
        return (T)reader.GetValue(reader.GetOrdinal(columnName));
    }

    protected static T? GetNullableValue<T>(DbDataReader reader, string columnName)
        where T : struct
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : (T)reader.GetValue(ordinal);
    }

    protected static string? GetNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : (string)reader.GetValue(ordinal);
    }

    protected static DateOnly? GetNullableDateOnly(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => throw new InvalidOperationException(
                $"Column '{columnName}' did not contain a date value.")
        };
    }
}
