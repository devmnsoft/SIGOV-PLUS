using Dapper;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public DatabaseHealthCheck(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string Name => "db";
    public bool IncludeInReady => true;

    public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("select current_database();", cancellationToken: cancellationToken)).ConfigureAwait(false) ?? "unknown";
        var schemaExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists (select 1 from information_schema.schemata where schema_name = 'sigov');", cancellationToken: cancellationToken)).ConfigureAwait(false);
        var details = new Dictionary<string, object?> { ["database"] = database, ["schema"] = "sigov", ["schemaExists"] = schemaExists };
        return schemaExists
            ? HealthCheckResult.Healthy(Name, "Banco sigov acessível.", details)
            : HealthCheckResult.Unhealthy(Name, "Schema sigov indisponível.", details);
    }
}
