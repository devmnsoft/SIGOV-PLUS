-- Pós-RC 27B.2: reconciliação aditiva do contrato operacional de Tarefas.
alter table sigov.tarefa add column if not exists version bigint not null default 1;
alter table sigov.tarefa add column if not exists prazo_em timestamptz null;
alter table sigov.tarefa add column if not exists entidade text null;
alter table sigov.tarefa add column if not exists entidade_id text null;
alter table sigov.tarefa add column if not exists concluida_em timestamptz null;
alter table sigov.tarefa add column if not exists cancelada_em timestamptz null;
alter table sigov.tarefa add column if not exists motivo_cancelamento text null;

DO $$
BEGIN
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='prazo_at') then
    execute 'update sigov.tarefa set prazo_em = coalesce(prazo_em, prazo_at) where prazo_em is null';
    comment on column sigov.tarefa.prazo_at is 'LEGADO: use prazo_em; mantida para compatibilidade nesta versão.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='entidade_tipo') then
    execute 'update sigov.tarefa set entidade = coalesce(entidade, entidade_tipo) where entidade is null';
    comment on column sigov.tarefa.entidade_tipo is 'LEGADO: use entidade; mantida para compatibilidade nesta versão.';
  end if;
END $$;

create index if not exists ix_tarefa_tenant_status on sigov.tarefa(tenant_id, status) where is_deleted = false;
create index if not exists ix_tarefa_tenant_responsavel_prazo on sigov.tarefa(tenant_id, responsavel_id, prazo_em) where is_deleted = false;
create index if not exists ix_tarefa_tenant_version on sigov.tarefa(tenant_id, version) where is_deleted = false;
create index if not exists ix_tarefa_tenant_entidade on sigov.tarefa(tenant_id, entidade, entidade_id) where is_deleted = false;

alter table sigov.tarefa_vinculo add column if not exists entidade text null;
alter table sigov.tarefa_vinculo add column if not exists entidade_id text null;
DO $$
BEGIN
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa_vinculo' and column_name='tipo') then
    execute 'update sigov.tarefa_vinculo set entidade = coalesce(entidade, tipo) where entidade is null';
    comment on column sigov.tarefa_vinculo.tipo is 'LEGADO: use entidade; mantida para compatibilidade nesta versão.';
  end if;
END $$;
