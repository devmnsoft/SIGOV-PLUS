namespace Sigov.Infrastructure.Outbox;

public static class OutboxSqlQueries
{
    public const string StartJob = "insert into sigov.integracao_job_execucao (job_nome,status,inicio_at,correlation_id) values ('Sigov.Worker.Outbox','PROCESSANDO',now(),@CorrelationId) returning id;";

    public const string FetchPending = @"update sigov.fila_evento f
set status = 'PROCESSANDO', updated_at = now()
from (
    select id
    from sigov.fila_evento
    where tenant_id is not null
      and dead_letter = false
      and status in ('PENDENTE','ERRO')
      and (proxima_tentativa_at is null or proxima_tentativa_at <= now())
    order by prioridade asc, created_at asc
    limit @BatchSize
    for update skip locked
) next
where f.id = next.id
returning f.id, f.tenant_id as TenantId, f.tipo_evento as TipoEvento, f.payload::text as Payload, f.tentativas as Tentativas, f.max_tentativas as MaxTentativas, f.correlation_id as CorrelationId;
";

    public const string MarkProcessed = @"update sigov.fila_evento
set status='PROCESSADO', processado_at=now(), erro=null, updated_at=now()
where id=@Id and tenant_id=@TenantId;
insert into sigov.integracao_log (tenant_id,direcao,tipo_evento,status,request_resumo,correlation_id)
values (@TenantId,'OUTBOX',@TipoEvento,'PROCESSADO',jsonb_build_object('eventoId',@Id),@CorrelationId);
";

    public const string MarkFailure = @"update sigov.fila_evento
set status = case when @DeadLetter then 'DEAD_LETTER' else 'ERRO' end,
    tentativas = @Tentativas,
    proxima_tentativa_at = case when @DeadLetter then null else now() + (@DelaySeconds * interval '1 second') end,
    dead_letter = @DeadLetter,
    erro = @Erro,
    updated_at = now()
where id = @Id and tenant_id = @TenantId;
insert into sigov.integracao_erro (tenant_id,tipo_erro,mensagem,detalhe,correlation_id)
values (@TenantId,'OUTBOX',@Erro,jsonb_build_object('eventoId',@Id,'tipoEvento',@TipoEvento,'deadLetter',@DeadLetter),@CorrelationId);
";

    public const string CompleteJob = "update sigov.integracao_job_execucao set status='PROCESSADO',fim_at=now(),itens_processados=@Processed where id=@JobId;";

    public const string FailJob = "update sigov.integracao_job_execucao set status='ERRO',fim_at=now(),erro=@Erro,itens_processados=@Processed where id=@JobId;";
}
