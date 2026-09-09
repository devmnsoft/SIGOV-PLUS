using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Sigov.Infrastructure.Persistence.Dapper;

public sealed class NpgsqlConnectionFactory
{
    private readonly string? _connectionString;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        _connectionString = connectionString;
    }

    public NpgsqlConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada. Em Production use variável de ambiente ou secret manager.");
        }

        return new NpgsqlConnection(_connectionString);
    }
}
