using System.Data.Common;

namespace Application.Abstractions.Database;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
