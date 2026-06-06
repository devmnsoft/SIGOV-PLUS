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
        Guid correlationId = Guid.NewGuid();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DapperContext>();
            using var connection = context.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            var jobId = await connection.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.integracao_job_execucao (job_nome,status,inicio_at,correlation_id) values ('Sigov.Worker.Outbox','PROCESSANDO',now(),@CorrelationId) returning id;", new { CorrelationId = correlationId }, tx, cancellationToken: cancellationToken)).ConfigureAwait(false);
            const string fetchSql = """
                update sigov.fila_evento f
                set status = 'PROCESSANDO', updated_at = now()
                from (
                    select id
                    from sigov.fila_evento
                    where tenant_id is not null
                      and dead_letter = false
                      and status in ('PENDENTE','ERRO')
                      and (proxima_tentativa_at is null or proxima_tentativa_at <= now())
                    order by prioridade asc, created_at asc
                    limit 20
                    for update skip locked
                ) next
                where f.id = next.id
                returning f.id, f.tenant_id as TenantId, f.tipo_evento as TipoEvento, f.payload::text as Payload, f.tentativas as Tentativas, f.max_tentativas as MaxTentativas, f.correlation_id as CorrelationId;
                """;
            var rows = (await connection.QueryAsync<OutboxRow>(new CommandDefinition(fetchSql, tx, cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();
            tx.Commit();
            var processed = 0;
            foreach (var row in rows)
            {
                try
                {
                    await HandleEventAsync(row, cancellationToken).ConfigureAwait(false);
                    await connection.ExecuteAsync(new CommandDefinition("update sigov.fila_evento set status='PROCESSADO',processado_at=now(),erro=null,updated_at=now() where id=@Id and tenant_id=@TenantId; insert into sigov.integracao_log (tenant_id,direcao,tipo_evento,status,request_resumo,correlation_id) values (@TenantId,'OUTBOX',@TipoEvento,'PROCESSADO',jsonb_build_object('eventoId',@Id),@CorrelationId);", new { row.Id, row.TenantId, row.TipoEvento, row.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                    processed++;
                    _logger.LogInformation("Outbox processada. EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId}", row.Id, row.TenantId, row.TipoEvento, row.CorrelationId);
                }
                catch (Exception ex)
                {
                    await MarkFailureAsync(connection, row, ex.Message, cancellationToken).ConfigureAwait(false);
                    _logger.LogError(ex, "Falha em evento outbox. EventId={EventId} TenantId={TenantId} TipoEvento={TipoEvento} CorrelationId={CorrelationId}", row.Id, row.TenantId, row.TipoEvento, row.CorrelationId);
                }
            }
            await connection.ExecuteAsync(new CommandDefinition("update sigov.integracao_job_execucao set status='PROCESSADO',fim_at=now(),itens_processados=@Processed where id=@JobId;", new { Processed = processed, JobId = jobId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no ciclo do worker de outbox; o próximo ciclo continuará sem derrubar a aplicação. CorrelationId={CorrelationId}", correlationId);
        }
    }

    private static Task HandleEventAsync(OutboxRow row, CancellationToken cancellationToken)
    {
        _ = row.TipoEvento switch
        {
            "WebhookEnviado" => true,
            "RemessaOficialGerada" => true,
            "RelatorioExecutado" => true,
            "SaasTenantProvisionado" => true,
            "FolhaIntegracaoFinanceiraSolicitada" => true,
            "PagamentoTributarioRegistrado" => true,
            _ => true
        };
        return Task.CompletedTask.WaitAsync(cancellationToken);
    }

    private static async Task MarkFailureAsync(System.Data.IDbConnection connection, OutboxRow row, string erro, CancellationToken cancellationToken)
    {
        var nextTentativas = row.Tentativas + 1;
        var deadLetter = nextTentativas >= row.MaxTentativas;
        var delay = nextTentativas switch { 1 => TimeSpan.FromMinutes(1), 2 => TimeSpan.FromMinutes(5), 3 => TimeSpan.FromMinutes(15), _ => TimeSpan.FromHours(1) };
        const string sql = """
            update sigov.fila_evento
            set status = case when @DeadLetter then 'DEAD_LETTER' else 'ERRO' end,
                tentativas = @Tentativas,
                proxima_tentativa_at = case when @DeadLetter then null else now() + (@DelaySeconds * interval '1 second') end,
                dead_letter = @DeadLetter,
                erro = @Erro,
                updated_at = now()
            where id = @Id and tenant_id = @TenantId;
            insert into sigov.integracao_erro (tenant_id,tipo_erro,mensagem,detalhe,correlation_id)
            values (@TenantId,'OUTBOX',@Erro,jsonb_build_object('eventoId',@Id,'tipoEvento',@TipoEvento,'deadLetter',@DeadLetter),@CorrelationId);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { row.Id, row.TenantId, row.TipoEvento, Tentativas = nextTentativas, DeadLetter = deadLetter, Erro = erro, DelaySeconds = (int)delay.TotalSeconds, row.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed record OutboxRow(long Id, long TenantId, string TipoEvento, string Payload, int Tentativas, int MaxTentativas, Guid? CorrelationId);
}
