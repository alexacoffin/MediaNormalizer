using System.Data.Common;
using Application.Abstractions.Database;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Database;

public sealed class SqlConnectionFactory(string? connectionString) : IDbConnectionFactory
{
    private readonly string? connectionString = connectionString;

    public DbConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database access requires the 'ConnectionStrings:MediaNormalizer' connection string.");
        }

        return new SqlConnection(connectionString);
    }
}
