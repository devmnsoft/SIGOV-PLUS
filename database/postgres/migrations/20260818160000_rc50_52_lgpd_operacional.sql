-- RC50.52: completa o fluxo operacional LGPD sem remover dados existentes.
set search_path to sigov;

alter table sigov.solicitacao_titular add column if not exists tenant_id bigint null;
alter table sigov.solicitacao_titular add column if not exists protocolo varchar(80) null;
alter table sigov.solicitacao_titular add column if not exists resposta text null;
alter table sigov.solicitacao_titular add column if not exists is_deleted boolean not null default false;
create sequence if not exists sigov.lgpd_protocolo_seq;

update sigov.solicitacao_titular s
set tenant_id = e.tenant_id
from sigov.entidade e
where s.tenant_id is null and e.id = s.entidade_id and e.tenant_id is not null;

create unique index if not exists ux_solicitacao_titular_tenant_protocolo
    on sigov.solicitacao_titular (tenant_id, protocolo)
    where protocolo is not null and not is_deleted;

create table if not exists sigov.lgpd_incidente_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    incidente_id bigint not null references sigov.lgpd_incidente(id),
    descricao text not null,
    created_by bigint null,
    correlation_id varchar(100) not null,
    created_at timestamptz not null default now()
);
create index if not exists ix_lgpd_incidente_evento_tenant_incidente
    on sigov.lgpd_incidente_evento (tenant_id, incidente_id, created_at desc);
