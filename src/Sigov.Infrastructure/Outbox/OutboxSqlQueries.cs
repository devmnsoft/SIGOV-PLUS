namespace Sigov.Infrastructure.Outbox;

public static class OutboxSqlQueries
{
    public const string StartJob = "insert into sigov.integracao_job_execucao (job_nome,status,inicio_at,correlation_id) values ('Sigov.Worker.Outbox','PROCESSANDO',now(),@CorrelationId) returning id;";

    public const string FetchPending = @"update sigov.outbox_evento f
set status = 'PROCESSANDO', updated_at = now()
from (
    select id
    from sigov.outbox_evento
    where tenant_id is not null
      and is_deleted = false
      and status in ('PENDENTE','ERRO')
      and (proxima_tentativa_at is null or proxima_tentativa_at <= now())
    order by created_at asc
    limit @BatchSize
    for update skip locked
) next
where f.id = next.id
returning f.id, f.tenant_id as TenantId, f.evento as TipoEvento, f.payload::text as Payload, f.tentativas as Tentativas, 5 as MaxTentativas, f.correlation_id as CorrelationId;
";

    public const string MarkProcessed = @"update sigov.outbox_evento
set status='ENTREGUE', updated_at=now(), erro_mascarado=null
where id=@Id and tenant_id=@TenantId;
insert into sigov.webhook_entrega (tenant_id,outbox_evento_id,evento,endpoint,status,http_status,tentativa,payload_mascarado,correlation_id,delivered_at)
values (@TenantId,@Id,@TipoEvento,'outbox-worker','ENTREGUE',200,0,jsonb_build_object('eventoId',@Id),@CorrelationId,now());
";

    public const string MarkFailure = @"update sigov.outbox_evento
set status = case when @DeadLetter then 'FALHOU' else 'ERRO' end,
    tentativas = @Tentativas,
    proxima_tentativa_at = case when @DeadLetter then null else now() + (@DelaySeconds * interval '1 second') end,
    erro_mascarado = left(@Erro, 500),
    updated_at = now()
where id = @Id and tenant_id = @TenantId;
insert into sigov.webhook_entrega (tenant_id,outbox_evento_id,evento,endpoint,status,tentativa,erro_mascarado,payload_mascarado,correlation_id)
values (@TenantId,@Id,@TipoEvento,'outbox-worker',case when @DeadLetter then 'FALHOU' else 'ERRO' end,@Tentativas,left(@Erro,500),jsonb_build_object('eventoId',@Id),@CorrelationId);
";

    public const string CompleteJob = "update sigov.integracao_job_execucao set status='PROCESSADO',fim_at=now(),itens_processados=@Processed where id=@JobId;";

    public const string FailJob = "update sigov.integracao_job_execucao set status='ERRO',fim_at=now(),erro=@Erro,itens_processados=@Processed where id=@JobId;";
}
