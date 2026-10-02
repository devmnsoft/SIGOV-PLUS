-- Pós-RC 27B.3: contrato operacional canônico, aditivo e idempotente.
create schema if not exists sigov;

alter table sigov.tarefa add column if not exists descricao text null;
alter table sigov.tarefa add column if not exists status text;
alter table sigov.tarefa add column if not exists prioridade text;
alter table sigov.tarefa add column if not exists responsavel_id bigint null;
alter table sigov.tarefa add column if not exists prazo_em timestamptz null;
alter table sigov.tarefa add column if not exists origem text null;
alter table sigov.tarefa add column if not exists entidade text null;
alter table sigov.tarefa add column if not exists entidade_id text null;
alter table sigov.tarefa add column if not exists version bigint not null default 1;
alter table sigov.tarefa add column if not exists concluida_em timestamptz null;
alter table sigov.tarefa add column if not exists cancelada_em timestamptz null;
alter table sigov.tarefa add column if not exists motivo_cancelamento text null;
alter table sigov.tarefa add column if not exists created_at timestamptz not null default now();
alter table sigov.tarefa add column if not exists created_by bigint null;
alter table sigov.tarefa add column if not exists updated_at timestamptz not null default now();
alter table sigov.tarefa add column if not exists updated_by bigint null;
alter table sigov.tarefa add column if not exists is_deleted boolean not null default false;
alter table sigov.tarefa add column if not exists correlation_id uuid null;

do $$
begin
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='data_limite') then
    execute 'update sigov.tarefa set prazo_em = coalesce(prazo_em, data_limite) where prazo_em is null';
    comment on column sigov.tarefa.data_limite is 'LEGADO: use prazo_em; mantida para compatibilidade.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='prazo_at') then
    execute 'update sigov.tarefa set prazo_em = coalesce(prazo_em, prazo_at) where prazo_em is null';
    comment on column sigov.tarefa.prazo_at is 'LEGADO: use prazo_em; mantida para compatibilidade.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='entidade_tipo') then
    execute 'update sigov.tarefa set entidade = coalesce(entidade, entidade_tipo) where entidade is null';
    comment on column sigov.tarefa.entidade_tipo is 'LEGADO: use entidade; mantida para compatibilidade.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='concluida_at') then
    execute 'update sigov.tarefa set concluida_em = coalesce(concluida_em, concluida_at) where concluida_em is null';
    comment on column sigov.tarefa.concluida_at is 'LEGADO: use concluida_em; mantida para compatibilidade.';
  end if;
end $$;

update sigov.tarefa set status = 'ABERTA' where status is null or status in ('PENDENTE', 'pendente');
update sigov.tarefa set prioridade = 'NORMAL' where prioridade is null or btrim(prioridade) = '';
alter table sigov.tarefa alter column status set default 'ABERTA';
alter table sigov.tarefa alter column status set not null;
alter table sigov.tarefa alter column prioridade set default 'NORMAL';
alter table sigov.tarefa alter column prioridade set not null;

create index if not exists ix_tarefa_tenant_status on sigov.tarefa(tenant_id, status) where is_deleted = false;
create index if not exists ix_tarefa_tenant_responsavel_prazo on sigov.tarefa(tenant_id, responsavel_id, prazo_em) where is_deleted = false;
create index if not exists ix_tarefa_tenant_version on sigov.tarefa(tenant_id, version) where is_deleted = false;
create index if not exists ix_tarefa_tenant_entidade on sigov.tarefa(tenant_id, entidade, entidade_id) where is_deleted = false;

alter table sigov.notificacao add column if not exists tenant_id bigint;
alter table sigov.notificacao add column if not exists tipo text;
alter table sigov.notificacao add column if not exists titulo text;
alter table sigov.notificacao add column if not exists mensagem text;
alter table sigov.notificacao add column if not exists modulo text null;
alter table sigov.notificacao add column if not exists prioridade text not null default 'NORMAL';
alter table sigov.notificacao add column if not exists origem text null;
alter table sigov.notificacao add column if not exists entidade text null;
alter table sigov.notificacao add column if not exists entidade_id text null;
alter table sigov.notificacao add column if not exists status text not null default 'CRIADA';
alter table sigov.notificacao add column if not exists created_at timestamptz not null default now();
alter table sigov.notificacao add column if not exists created_by bigint null;
alter table sigov.notificacao add column if not exists updated_at timestamptz not null default now();
alter table sigov.notificacao add column if not exists updated_by bigint null;
alter table sigov.notificacao add column if not exists is_deleted boolean not null default false;
alter table sigov.notificacao add column if not exists correlation_id uuid null;

alter table sigov.notificacao_usuario add column if not exists tenant_id bigint;
alter table sigov.notificacao_usuario add column if not exists notificacao_id bigint;
alter table sigov.notificacao_usuario add column if not exists usuario_id bigint;
alter table sigov.notificacao_usuario add column if not exists tipo text null;
alter table sigov.notificacao_usuario add column if not exists titulo text null;
alter table sigov.notificacao_usuario add column if not exists lida boolean not null default false;
alter table sigov.notificacao_usuario add column if not exists lida_em timestamptz null;
alter table sigov.notificacao_usuario add column if not exists arquivada boolean not null default false;
alter table sigov.notificacao_usuario add column if not exists created_at timestamptz not null default now();
alter table sigov.notificacao_usuario add column if not exists created_by bigint null;
alter table sigov.notificacao_usuario add column if not exists updated_at timestamptz not null default now();
alter table sigov.notificacao_usuario add column if not exists updated_by bigint null;
alter table sigov.notificacao_usuario add column if not exists is_deleted boolean not null default false;
alter table sigov.notificacao_usuario add column if not exists correlation_id uuid null;

alter table sigov.tarefa_vinculo add column if not exists entidade text null;
alter table sigov.tarefa_vinculo add column if not exists entidade_id text null;

do $$
begin
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='notificacao_usuario' and column_name='lida_at') then
    execute 'update sigov.notificacao_usuario set lida = (lida_at is not null), lida_em = coalesce(lida_em, lida_at)';
    comment on column sigov.notificacao_usuario.lida_at is 'LEGADO: use lida/lida_em; mantida para compatibilidade.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa_vinculo' and column_name='tipo') then
    execute 'update sigov.tarefa_vinculo set entidade = coalesce(entidade, tipo) where entidade is null';
    comment on column sigov.tarefa_vinculo.tipo is 'LEGADO: use entidade; mantida para compatibilidade.';
  end if;
end $$;
