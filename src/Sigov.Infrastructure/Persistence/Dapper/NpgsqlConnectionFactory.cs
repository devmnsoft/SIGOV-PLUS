using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Sigov.Infrastructure.Persistence.Dapper;

public sealed class NpgsqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
