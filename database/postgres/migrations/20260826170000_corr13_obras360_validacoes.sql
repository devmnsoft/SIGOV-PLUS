-- CORR13: validações defensivas do Obras360, sem alteração destrutiva de objetos publicados.
create schema if not exists sigov;

do $$
begin
  if not exists (select 1 from pg_constraint where conname='ck_corr13_obra_coordenadas' and conrelid='sigov.obras_obra'::regclass) then
    alter table sigov.obras_obra add constraint ck_corr13_obra_coordenadas check ((latitude is null or latitude between -90 and 90) and (longitude is null or longitude between -180 and 180)) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_cronograma_status' and conrelid='sigov.obras_cronograma'::regclass) then
    alter table sigov.obras_cronograma add constraint ck_corr13_cronograma_status check (status in ('RASCUNHO','ATIVO','CONCLUIDO','CANCELADO')) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_medicao_percentual_periodo' and conrelid='sigov.obras_medicao'::regclass) then
    alter table sigov.obras_medicao add constraint ck_corr13_medicao_percentual_periodo check ((percentual_fisico is null or percentual_fisico between 0 and 100) and (periodo_inicio is null or periodo_fim is null or periodo_fim>=periodo_inicio)) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_medicao_saldos' and conrelid='sigov.obras_medicao'::regclass) then
    alter table sigov.obras_medicao add constraint ck_corr13_medicao_saldos check (saldo_contratual_antes>=0 and saldo_contratual_depois>=0 and saldo_contratual_depois<=saldo_contratual_antes) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_diario_conteudo_aprovado' and conrelid='sigov.obras_diario'::regclass) then
    alter table sigov.obras_diario add constraint ck_corr13_diario_conteudo_aprovado check (status not in ('ENVIADO','APROVADO') or (data is not null and responsavel_id is not null and nullif(btrim(coalesce(atividades_executadas,descricao)), '') is not null)) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_ocorrencia_dominio' and conrelid='sigov.obras_ocorrencia'::regclass) then
    alter table sigov.obras_ocorrencia add constraint ck_corr13_ocorrencia_dominio check (status in ('ABERTA','EM_TRATAMENTO','RESOLVIDA','CANCELADA') and origem in ('DIARIO','FISCALIZACAO','VISTORIA','MANUAL') and severidade in ('BAIXA','MEDIA','ALTA','CRITICA')) not valid;
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_corr13_nc_status' and conrelid='sigov.obras_nao_conformidade'::regclass) then
    alter table sigov.obras_nao_conformidade add constraint ck_corr13_nc_status check (status in ('ABERTA','EM_CORRECAO','CORRIGIDA','CANCELADA')) not valid;
  end if;
end $$;

create index if not exists ix_corr13_ocorrencia_contexto_prazo on sigov.obras_ocorrencia(tenant_id,entidade_id,exercicio_id,obra_id,status,prazo) where not is_deleted;
create index if not exists ix_corr13_ordem_contexto_prazo on sigov.obras_ordem_servico(tenant_id,entidade_id,exercicio_id,obra_id,status,prazo) where not is_deleted;
