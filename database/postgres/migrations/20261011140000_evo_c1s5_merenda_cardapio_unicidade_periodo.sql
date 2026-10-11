-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 5 (educacao/merenda base): cardápio de merenda
-- unicidade de período vigente por escola × data de início.
-- Um cardápio VIGENTE (não terminal) por escola e data de início, independentemente
-- da esfera (regra 24): estados terminais (ENCERRADA/ENCERRADO/CANCELADA/CANCELADO)
-- liberam o período para novo cardápio, como a política de unicidade de matrícula da
-- Sprint 3. O comando tipado (POST /api/educacao/merenda/cardapios/completo) checa
-- antes de gravar (falha nomeada 409); o índice é o backstop transacional.
-- Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_merenda_cardapio_tenant_escola_inicio
    on sigov.educacao_merenda_cardapio (tenant_id, escola_id, data_inicio)
    where is_deleted = false
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Post-condição explícita (regra 13): sem o índice, falha nomeada.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_merenda_cardapio_tenant_escola_inicio') then
        raise exception 'S5-POSTCOND: indice ux_merenda_cardapio_tenant_escola_inicio ausente';
    end if;
end $post$;
