-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 9 (educacao/fundeb_custos_indicadores): unicao de
-- codigo vigente por tenant para parametros FDEB (ex.: taxa minima de
-- aplicacao) e centros de custo, e uma receita FDEB vigente por
-- competencia/fonte por tenant — backstops transacionais dos comandos
-- tipados de parametro/receita/aplicacao/centro de custo. Regra 24: nada
-- municipal hardcoded — as chaves sao por tenant, validas para qualquer
-- esfera/orgao; a taxa minima de aplicacao (LDB art. 70 e equivalentes de
-- cada esfera) vem de parametro do banco, nunca hardcoded. Estados terminais
-- (ENCERRADA/ENCERRADO/CANCELADA/CANCELADO) liberam a unicidade, como nas
-- politicas das Sprints 3-8. Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_fundeb_parametro_tenant_codigo
    on sigov.educacao_fundeb_parametro (tenant_id, coalesce(codigo, ''::varchar))
    where is_deleted = false
      and ativo = true
      and codigo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_fundeb_receita_tenant_competencia_fonte
    on sigov.educacao_fundeb_receita (tenant_id, coalesce(competencia, '2999-12-31'::date), coalesce(tipo, ''::varchar))
    where is_deleted = false
      and ativo = true
      and competencia is not null
      and tipo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_custo_centro_tenant_codigo
    on sigov.educacao_custo_centro (tenant_id, coalesce(codigo, ''::varchar))
    where is_deleted = false
      and ativo = true
      and codigo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Pos-condicao explicita (regra 13): sem os indices, falha nomeada.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_fundeb_parametro_tenant_codigo') then
        raise exception 'S9-POSTCOND: indice ux_fundeb_parametro_tenant_codigo ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_fundeb_receita_tenant_competencia_fonte') then
        raise exception 'S9-POSTCOND: indice ux_fundeb_receita_tenant_competencia_fonte ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_custo_centro_tenant_codigo') then
        raise exception 'S9-POSTCOND: indice ux_custo_centro_tenant_codigo ausente';
    end if;
end $post$;
