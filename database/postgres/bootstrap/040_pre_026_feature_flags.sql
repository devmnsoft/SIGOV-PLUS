-- SIGOV+ RC38E
-- Migrations de domínio mais antigas gravam feature flags por modulo_codigo e
-- feature_codigo. O contrato SaaS final exige feature_flag_def_id. Este preflight
-- antecipa as colunas de compatibilidade e resolve a definição canônica antes das
-- constraints, tanto na primeira quanto na segunda execução.

-- A migration 026 cria uma versão fundacional da view. Migrations Agro posteriores
-- acrescentam colunas. Na segunda passagem, CREATE OR REPLACE não pode remover essas
-- colunas; a view é removida aqui e recriada imediatamente pela migration 026.
drop view if exists sigov.vw_agro_dashboard;

alter table sigov.tenant_feature_flag
    add column if not exists modulo_codigo varchar(80),
    add column if not exists feature_codigo varchar(120),
    add column if not exists habilitada boolean not null default false,
    add column if not exists parametros_json jsonb not null default '{}'::jsonb;

create or replace function sigov.fn_tenant_feature_flag_resolver_definicao()
returns trigger
language plpgsql
as $$
begin
    if new.feature_flag_def_id is null and new.feature_codigo is not null then
        select f.id
          into new.feature_flag_def_id
          from sigov.feature_flag_def f
         where f.codigo = new.feature_codigo
           and f.ativo = true
           and f.is_deleted = false
         order by f.id
         limit 1;
    end if;

    if new.feature_flag_def_id is not null then
        select coalesce(new.modulo_codigo, f.modulo),
               coalesce(new.feature_codigo, f.codigo)
          into new.modulo_codigo, new.feature_codigo
          from sigov.feature_flag_def f
         where f.id = new.feature_flag_def_id;
    end if;

    if new.feature_flag_def_id is null then
        raise exception 'Definição não encontrada para a feature flag %.', coalesce(new.feature_codigo, '<nula>')
            using errcode = '23503';
    end if;

    return new;
end $$;

drop trigger if exists trg_tenant_feature_flag_resolver_definicao on sigov.tenant_feature_flag;
create trigger trg_tenant_feature_flag_resolver_definicao
before insert or update of feature_flag_def_id, feature_codigo, modulo_codigo
on sigov.tenant_feature_flag
for each row
execute function sigov.fn_tenant_feature_flag_resolver_definicao();
