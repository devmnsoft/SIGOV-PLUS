create table if not exists sigov.enterprise_tenant_mapping (
    id bigserial primary key,
    core_tenant_id bigint not null,
    enterprise_tenant_id uuid not null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    constraint uq_enterprise_tenant_mapping_core unique (core_tenant_id),
    constraint uq_enterprise_tenant_mapping_enterprise unique (enterprise_tenant_id),
    constraint ck_enterprise_tenant_mapping_not_empty check (enterprise_tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);

create index if not exists idx_enterprise_tenant_mapping_core_tenant_id on sigov.enterprise_tenant_mapping (core_tenant_id);
create index if not exists idx_enterprise_tenant_mapping_enterprise_tenant_id on sigov.enterprise_tenant_mapping (enterprise_tenant_id);
create index if not exists idx_enterprise_tenant_mapping_ativo on sigov.enterprise_tenant_mapping (ativo);
