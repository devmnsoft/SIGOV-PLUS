using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Worker;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DapperContext>();
            using var connection = context.CreateConnection();
            const string sql = """
                update sigov.fila_evento
                set status = 'PROCESSADO', processado_at = now(), updated_at = now()
                where id in (
                    select id
                    from sigov.fila_evento
                    where tenant_id is not null
                      and status = 'PENDENTE'
                      and (proxima_tentativa_at is null or proxima_tentativa_at <= now())
                    order by created_at
                    limit 20
                )
                returning id, tenant_id as TenantId, tipo_evento as TipoEvento, correlation_id as CorrelationId;
                """;
            var processed = await connection.QueryAsync<OutboxRow>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            foreach (var row in processed)
            {
                _logger.LogInformation("Outbox processada. EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId}", row.Id, row.TenantId, row.TipoEvento, row.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no ciclo do worker de outbox; o próximo ciclo continuará sem derrubar a aplicação.");
        }
    }

    private sealed record OutboxRow(long Id, long TenantId, string TipoEvento, Guid? CorrelationId);
}
