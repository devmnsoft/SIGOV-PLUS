-- RC46: base transversal idempotente para workflows operacionais.
create table if not exists sigov.timeline_evento (
  id bigserial primary key,
  tenant_id bigint not null,
  entidade_id bigint null,
  exercicio_id bigint null,
  modulo varchar(40) not null,
  entidade varchar(80) not null,
  entidade_registro_id bigint not null,
  acao varchar(80) not null,
  descricao varchar(500) not null,
  severidade varchar(20) not null default 'INFO',
  usuario_id bigint null,
  correlation_id uuid not null,
  detalhes_json jsonb null,
  created_at timestamptz not null default now()
);

create index if not exists ix_timeline_evento_escopo
  on sigov.timeline_evento (tenant_id, entidade, entidade_registro_id, created_at desc);

create unique index if not exists ux_protocolo_numero_exercicio
  on sigov.protocolo (tenant_id, exercicio, numero)
  where coalesce(is_deleted, false) = false;

alter table sigov.protocolo add column if not exists entidade_id bigint null;
alter table sigov.protocolo add column if not exists exercicio_id bigint null;
alter table sigov.protocolo_movimento add column if not exists entidade_id bigint null;
alter table sigov.protocolo_movimento add column if not exists exercicio_id bigint null;
alter table sigov.protocolo_movimento add column if not exists destino varchar(160) null;

create index if not exists ix_protocolo_escopo_status
  on sigov.protocolo (tenant_id, entidade_id, exercicio_id, status, created_at desc);

-- A função serializa a numeração por tenant/exercício sem depender de MAX(id).
create or replace function sigov.proximo_numero_protocolo(p_tenant_id bigint, p_exercicio integer)
returns varchar language plpgsql as $$
declare v_sequencia bigint;
begin
  perform pg_advisory_xact_lock(p_tenant_id, p_exercicio);
  select coalesce(max(split_part(numero, '/', 1)::bigint), 0) + 1
    into v_sequencia
    from sigov.protocolo
   where tenant_id = p_tenant_id
     and exercicio = p_exercicio
     and numero ~ '^[0-9]+/[0-9]{4}$';
  return lpad(v_sequencia::text, 6, '0') || '/' || p_exercicio::text;
end $$;
