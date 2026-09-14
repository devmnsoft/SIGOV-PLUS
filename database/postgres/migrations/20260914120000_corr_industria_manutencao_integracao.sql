-- Integração idempotente e rastreável entre parada industrial e manutenção canônica.
alter table sigov.manutencao_ordem_servico
    add column if not exists origem_tipo varchar(40),
    add column if not exists origem_id bigint;

do $$
begin
    if not exists (
        select 1 from pg_constraint
        where conrelid = 'sigov.manutencao_ordem_servico'::regclass
          and conname = 'ck_manutencao_os_origem_completa'
    ) then
        alter table sigov.manutencao_ordem_servico
            add constraint ck_manutencao_os_origem_completa
            check ((origem_tipo is null) = (origem_id is null)) not valid;
    end if;
end $$;

create unique index if not exists ux_manutencao_os_origem
    on sigov.manutencao_ordem_servico(tenant_id, origem_tipo, origem_id)
    where origem_tipo is not null and origem_id is not null;

create index if not exists ix_industria_parada_os
    on sigov.industria_parada_producao(tenant_id, os_id)
    where gerou_os and os_id is not null;
