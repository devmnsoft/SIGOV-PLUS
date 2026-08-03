-- SIGOV+ RC38E
-- Migrations de domínio mais antigas gravam feature flags por modulo_codigo e
-- feature_codigo. O contrato SaaS final exige feature_flag_def_id. Este trigger
-- resolve a definição canônica antes da validação NOT NULL/foreign key.

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
