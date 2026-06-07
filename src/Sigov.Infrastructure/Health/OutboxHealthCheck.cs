using Dapper;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Health;

public sealed class OutboxHealthCheck : IHealthCheck
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public OutboxHealthCheck(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public string Name => "outbox";
    public bool IncludeInReady => true;

    public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = "select to_regclass('sigov.fila_evento') is not null;";
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var details = new Dictionary<string, object?> { ["queue"] = "sigov.fila_evento", ["tableExists"] = exists };
        return exists
            ? HealthCheckResult.Healthy(Name, "Fila outbox acessível.", details)
            : HealthCheckResult.Degraded(Name, "Tabela sigov.fila_evento não encontrada.", details);
    }
}
