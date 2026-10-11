-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 6 (educacao/merenda operação): distribuição de refeição
-- unicidade de data por cardápio + consumo real de estoque.
-- Uma distribuição VIGENTE (não terminal) por cardápio e data de atendimento
-- (regra 24: nada municipal hardcoded — a chave é cardápio × data, válida para
-- qualquer esfera/órgão). Estados terminais (ENCERRADA/ENCERRADO/CANCELADA/
-- CANCELADO) liberam a data para nova distribuição, como as políticas de
-- unicidade das Sprints 3 e 5. O comando tipado
-- (POST /api/educacao/merenda/distribuicoes) checa antes de gravar (falha
-- nomeada 409); o índice é o backstop transacional.
-- Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_merenda_distribuicao_tenant_cardapio_data
    on sigov.educacao_merenda_distribuicao (tenant_id, referencia_id, competencia)
    where is_deleted = false
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Post-condição explícita (regra 13): sem o índice, falha nomeada.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_merenda_distribuicao_tenant_cardapio_data') then
        raise exception 'S6-POSTCOND: indice ux_merenda_distribuicao_tenant_cardapio_data ausente';
    end if;
end $post$;
