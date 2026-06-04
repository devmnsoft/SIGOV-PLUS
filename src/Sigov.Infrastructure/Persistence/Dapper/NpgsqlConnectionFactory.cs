using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Sigov.Infrastructure.Persistence.Dapper;

public sealed class NpgsqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada. Em Production use variável de ambiente ou secret manager.");
        }
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
