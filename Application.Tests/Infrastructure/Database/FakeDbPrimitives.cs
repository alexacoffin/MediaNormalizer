using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Application.Tests.Infrastructure.Database;

internal sealed class FakeDbConnectionFactory : Application.Abstractions.Database.IDbConnectionFactory
{
    public FakeDbConnection Connection { get; } = new();

    public DbConnection CreateConnection() => Connection;
}

internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState state = ConnectionState.Closed;

    public List<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    public List<FakeDbCommand> Commands { get; } = [];

    public bool IsDisposed { get; private set; }

    public CancellationToken OpenCancellationToken { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => "Fake";

    public override string DataSource => "Fake";

    public override string ServerVersion => "1.0";

    public override ConnectionState State => state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Open() => state = ConnectionState.Open;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        OpenCancellationToken = cancellationToken;
        Open();
        return Task.CompletedTask;
    }

    public override void Close() => state = ConnectionState.Closed;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
    {
        var command = new FakeDbCommand(this, Rows);
        Commands.Add(command);
        return command;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
            Close();
        }

        base.Dispose(disposing);
    }
}

internal sealed class FakeDbCommand(FakeDbConnection connection, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    : DbCommand
{
    private readonly FakeDbParameterCollection parameters = new();

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; } = rows;

    public CancellationToken ExecuteCancellationToken { get; private set; }

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; }

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    [AllowNull]
    protected override DbConnection DbConnection
    {
        get => connection;
        set => throw new NotSupportedException();
    }

    protected override DbParameterCollection DbParameterCollection => parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => throw new NotSupportedException();

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FakeDbDataReader(Rows);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
        CommandBehavior behavior,
        CancellationToken cancellationToken)
    {
        ExecuteCancellationToken = cancellationToken;
        return Task.FromResult<DbDataReader>(new FakeDbDataReader(Rows));
    }
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType()
    {
        DbType = DbType.Object;
    }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> items = [];

    public override int Count => items.Count;

    public override object SyncRoot { get; } = new();

    public override int Add(object value)
    {
        items.Add((DbParameter)value);
        return items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear() => items.Clear();

    public override bool Contains(object value) => items.Contains((DbParameter)value);

    public override bool Contains(string value) => items.Any(parameter => parameter.ParameterName == value);

    public override void CopyTo(Array array, int index) => items.ToArray().CopyTo(array, index);

    public override IEnumerator GetEnumerator() => items.GetEnumerator();

    public override int IndexOf(object value) => items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) =>
        items.FindIndex(parameter => parameter.ParameterName == parameterName);

    public override void Insert(int index, object value) => items.Insert(index, (DbParameter)value);

    public override bool IsFixedSize => false;

    public override bool IsReadOnly => false;

    public override bool IsSynchronized => false;

    public override void Remove(object value) => items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => items[index];

    protected override DbParameter GetParameter(string parameterName) => items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => items[IndexOf(parameterName)] = value;
}

internal sealed class FakeDbDataReader(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) : DbDataReader
{
    private int rowIndex = -1;
    private bool isClosed;

    private IReadOnlyDictionary<string, object?> CurrentRow => rows[rowIndex];

    private IReadOnlyList<string> Columns => rows.Count == 0 ? [] : rows[0].Keys.ToArray();

    public override int Depth => 0;

    public override int FieldCount => Columns.Count;

    public override bool HasRows => rows.Count > 0;

    public override bool IsClosed => isClosed;

    public override int RecordsAffected => -1;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = (byte[])GetValue(ordinal);
        var available = Math.Max(0, bytes.Length - (int)dataOffset);
        var copyLength = Math.Min(available, length);
        if (buffer is not null)
        {
            Array.Copy(bytes, dataOffset, buffer, bufferOffset, copyLength);
        }

        return copyLength;
    }

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var chars = ((string)GetValue(ordinal)).ToCharArray();
        var available = Math.Max(0, chars.Length - (int)dataOffset);
        var copyLength = Math.Min(available, length);
        if (buffer is not null)
        {
            Array.Copy(chars, dataOffset, buffer, bufferOffset, copyLength);
        }

        return copyLength;
    }

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override Type GetFieldType(int ordinal) => GetValue(ordinal).GetType();

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetName(int ordinal) => Columns[ordinal];

    public override int GetOrdinal(string name) =>
        Array.FindIndex(Columns.ToArray(), column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override object GetValue(int ordinal) => CurrentRow[Columns[ordinal]] ?? DBNull.Value;

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var index = 0; index < count; index++)
        {
            values[index] = GetValue(index);
        }

        return count;
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

    public override bool NextResult() => false;

    public override bool Read()
    {
        if (rowIndex + 1 >= rows.Count)
        {
            return false;
        }

        rowIndex++;
        return true;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Read());

    public override DataTable? GetSchemaTable() => null;

    public override IEnumerator GetEnumerator()
    {
        return rows.GetEnumerator();
    }

    public override void Close() => isClosed = true;
}
