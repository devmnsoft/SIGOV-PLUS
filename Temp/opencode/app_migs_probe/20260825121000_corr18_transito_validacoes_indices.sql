
-- Migration corretiva: a migration FUNC18 publicada permanece imutável.
do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'ck_transito_sinalizacao_datas') then
    alter table sigov.transito_sinalizacao add constraint ck_transito_sinalizacao_datas
      check (data_ultima_vistoria is null or data_instalacao is null or data_ultima_vistoria >= data_instalacao);
  end if;
end $$;

create index if not exists ix_transito_auto_contexto_status_data
  on sigov.transito_auto_infracao(tenant_id, entity_id, status, data_hora_infracao) where deleted_at is null;
create index if not exists ix_transito_notificacao_contexto_status_vencimento
  on sigov.transito_notificacao(tenant_id, entity_id, status, data_vencimento) where deleted_at is null;
create index if not exists ix_transito_recurso_contexto_status
  on sigov.transito_recurso(tenant_id, entity_id, status) where deleted_at is null;
create index if not exists ix_transito_ocorrencia_contexto_status
  on sigov.transito_ocorrencia(tenant_id, entity_id, status) where deleted_at is null;
create index if not exists ix_transito_sinalizacao_contexto_conservacao
  on sigov.transito_sinalizacao(tenant_id, entity_id, estado_conservacao) where deleted_at is null;
create index if not exists ix_transito_autorizacao_contexto_validade
  on sigov.transito_autorizacao_transporte(tenant_id, entity_id, data_validade) where deleted_at is null;
create index if not exists ix_transito_vistoria_contexto_resultado
  on sigov.transito_vistoria_transporte(tenant_id, entity_id, resultado) where deleted_at is null;
create index if not exists ix_transito_credencial_contexto_validade
  on sigov.transito_credencial(tenant_id, entity_id, data_validade) where deleted_at is null;
