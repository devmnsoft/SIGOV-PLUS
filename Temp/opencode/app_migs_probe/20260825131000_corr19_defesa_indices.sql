-- CORR19 - índices operacionais idempotentes do FUNC19

create index if not exists ix_defesa_agente_matricula_cpf
    on sigov.defesa_agente (tenant_id, entity_id, matricula, cpf)
    where deleted_at is null;
do $$
begin
    if not exists (
        select 1 from sigov.defesa_recurso_operacional
        where deleted_at is null and patrimonio_codigo is not null
        group by tenant_id, entity_id, patrimonio_codigo having count(*) > 1
    ) then
        create unique index if not exists uq_defesa_recurso_patrimonio_ativo
            on sigov.defesa_recurso_operacional (tenant_id, entity_id, patrimonio_codigo)
            where deleted_at is null and patrimonio_codigo is not null;
    end if;
end $$;
create index if not exists ix_defesa_ocorrencia_status_prioridade_data
    on sigov.defesa_ocorrencia (tenant_id, entity_id, status, prioridade, data_hora_abertura desc)
    where deleted_at is null;
create index if not exists ix_defesa_area_nivel_status
    on sigov.defesa_area_risco (tenant_id, entity_id, nivel_risco, status)
    where deleted_at is null;
create index if not exists ix_defesa_acionamento_status
    on sigov.defesa_acionamento (tenant_id, entity_id, status)
    where deleted_at is null;
create index if not exists ix_defesa_vistoria_status_data
    on sigov.defesa_vistoria (tenant_id, entity_id, status, data_vistoria desc)
    where deleted_at is null;
create index if not exists ix_defesa_abrigo_status
    on sigov.defesa_abrigo (tenant_id, entity_id, status)
    where deleted_at is null;
create index if not exists ix_defesa_ordem_status_prioridade
    on sigov.defesa_ordem_servico (tenant_id, entity_id, status, prioridade)
    where deleted_at is null;
create index if not exists ix_defesa_auditoria_tenant_entity_data
    on sigov.defesa_auditoria (tenant_id, entity_id, created_at desc);
