-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 10 (educacao/indicadores + educacenso + projecao de
-- matricula): unicao vigente por tenant de (a) catalogo de indicadores por
-- codigo, (b) valor de indicador por indicador x ano, (c) projecao de
-- matricula por escola x ano e (d) lote Educacenso por ano — backstops
-- transacionais dos comandos tipados de indicador/valor/projecao/lote/
-- inconsistencia. Regra 24: nada municipal hardcoded — as chaves sao por
-- tenant, validas para qualquer esfera/orgao; a projecao atende planejamento
-- de oferta para municipios, estados e Uniao. Estados terminais
-- (ENCERRADA/ENCERRADO/CANCELADA/CANCELADO) liberam a unicidade, como nas
-- politicas das Sprints 3-9. Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_indicador_tenant_codigo
    on sigov.educacao_indicador (tenant_id, coalesce(codigo, ''::varchar))
    where is_deleted = false
      and ativo = true
      and codigo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_indicador_valor_tenant_ref_ano
    on sigov.educacao_indicador_valor (tenant_id, coalesce(referencia_id, 0), coalesce(ano_letivo, 0))
    where is_deleted = false
      and ativo = true
      and referencia_id is not null
      and ano_letivo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_projecao_matricula_tenant_escola_ano
    on sigov.educacao_projecao_matricula (tenant_id, coalesce(escola_id, 0), coalesce(ano_letivo, 0))
    where is_deleted = false
      and ativo = true
      and escola_id is not null
      and ano_letivo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_educacenso_lote_tenant_ano
    on sigov.educacao_educacenso_lote (tenant_id, coalesce(ano_letivo, 0))
    where is_deleted = false
      and ativo = true
      and ano_letivo is not null
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Pos-condicao explicita (regra 13): sem os indices, falha nomeada.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_indicador_tenant_codigo') then
        raise exception 'S10-POSTCOND: indice ux_indicador_tenant_codigo ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_indicador_valor_tenant_ref_ano') then
        raise exception 'S10-POSTCOND: indice ux_indicador_valor_tenant_ref_ano ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_projecao_matricula_tenant_escola_ano') then
        raise exception 'S10-POSTCOND: indice ux_projecao_matricula_tenant_escola_ano ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_educacenso_lote_tenant_ano') then
        raise exception 'S10-POSTCOND: indice ux_educacenso_lote_tenant_ano ausente';
    end if;
end $post$;
