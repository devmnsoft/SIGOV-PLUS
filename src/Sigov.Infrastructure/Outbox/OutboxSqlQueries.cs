namespace Sigov.Infrastructure.Outbox;

public static class OutboxSqlQueries
{
    public const string StartJob = "insert into sigov.integracao_job_execucao (job_nome,status,inicio_at,correlation_id) values ('Sigov.Worker.Outbox','PROCESSANDO',now(),@CorrelationId) returning id;";

    // RC-EVO-RH s9: alinhado ao schema aplicado de sigov.outbox_evento (colunas reais:
    // tipo_evento, erro, next_attempt_at); não referencia colunas de geração anterior da
    // fila que não existem nessa tabela. Os status são comparados sem diferenciar
    // maiúsculas porque os produtores históricos usam 'pendente' e convenções 'PENDING'.
    // RC-EVO-B §5: o claim usa next_attempt_at como LEASE (now()+@LeaseSeconds). Um worker que
    // morre no meio do processamento deixa a mensagem em PROCESSANDO; vencida a locação ela volta
    // a ser elegível (claim é idempotente pela chave compartilhada empenho.criar). Linhas antigas
    // pré-lease (PROCESSANDO sem vencimento) são retomadas após 30 minutos sem atualização.
    public const string FetchPending = @"update sigov.outbox_evento f
set status = 'PROCESSANDO',
    next_attempt_at = now() + (@LeaseSeconds * interval '1 second'),
    updated_at = now()
from (
    select id
    from sigov.outbox_evento
    where tenant_id is not null
      and (
        (lower(status) in ('pendente','pending','erro')
             and (next_attempt_at is null or next_attempt_at <= now()))
        or (lower(status) = 'processando' and next_attempt_at is not null and next_attempt_at <= now())
        or (lower(status) = 'processando' and next_attempt_at is null and updated_at < now() - interval '30 minutes')
      )
    order by created_at asc
    limit @BatchSize
    for update skip locked
) next
where f.id = next.id
returning f.id, f.tenant_id as TenantId, f.tipo_evento as TipoEvento, f.payload::text as Payload, f.tentativas as Tentativas, 5 as MaxTentativas, f.correlation_id as CorrelationId;
";

    public const string MarkProcessed = @"update sigov.outbox_evento
set status='ENTREGUE', processed_at=now(), updated_at=now(), erro=null
where id=@Id and tenant_id=@TenantId;
insert into sigov.webhook_entrega (tenant_id,outbox_evento_id,evento,endpoint,status,http_status,tentativa,payload_mascarado,correlation_id,delivered_at)
values (@TenantId,@Id,@TipoEvento,'outbox-worker','ENTREGUE',200,0,jsonb_build_object('eventoId',@Id),@CorrelationId,now());
";

    public const string MarkFailure = @"update sigov.outbox_evento
set status = case when @DeadLetter then 'FALHOU' else 'ERRO' end,
    tentativas = @Tentativas,
    attempts = @Tentativas,
    erro = left(@Erro, 500),
    next_attempt_at = case when @DeadLetter then null else now() + (@DelaySeconds * interval '1 second') end,
    updated_at = now()
where id = @Id and tenant_id = @TenantId;
insert into sigov.webhook_entrega (tenant_id,outbox_evento_id,evento,endpoint,status,tentativa,erro_mascarado,payload_mascarado,correlation_id)
values (@TenantId,@Id,@TipoEvento,'outbox-worker',case when @DeadLetter then 'FALHOU' else 'ERRO' end,@Tentativas,left(@Erro,500),jsonb_build_object('eventoId',@Id),@CorrelationId);
";

    public const string CompleteJob = "update sigov.integracao_job_execucao set status='PROCESSADO',fim_at=now(),itens_processados=@Processed where id=@JobId;";

    public const string FailJob = "update sigov.integracao_job_execucao set status='ERRO',fim_at=now(),erro=@Erro,itens_processados=@Processed where id=@JobId;";
}
