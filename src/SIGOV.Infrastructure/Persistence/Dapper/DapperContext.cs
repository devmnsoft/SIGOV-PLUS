using System.Data;

namespace SIGOV.Infrastructure.Persistence.Dapper;

public sealed class DapperContext
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public DapperContext(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public IDbConnection CreateConnection() => _connectionFactory.CreateConnection();
}
