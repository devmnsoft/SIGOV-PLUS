-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 7 (educacao/biblioteca circulacao): emprestimo,
-- devolucao e reserva com unicidade de vigencia por exemplar.
-- Um emprestimo VIGENTE (nao terminal) por exemplar (tenant) e uma reserva
-- vigente por exemplar (fila de tamanho 1 — simplificacao declarada) (regra 24:
-- nada municipal hardcoded — a chave e tenant x exemplar, valida para qualquer
-- esfera/orgao). Estados terminais (ENCERRADA/ENCERRADO/CANCELADA/CANCELADO)
-- liberam o exemplar para novo emprestimo/reserva, como nas politicas de
-- unicidade das Sprints 3, 5 e 6. Os comandos tipados
-- (POST /api/educacao/biblioteca/emprestimos/lancar,
-- POST /api/educacao/biblioteca/emprestimos/{id}/devolver e
-- POST /api/educacao/biblioteca/reservas/lancar) checam antes de gravar
-- (falhas nomeadas 409); os indices sao o backstop transacional.
-- Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_biblioteca_emprestimo_tenant_exemplar_vigencia
    on sigov.educacao_biblioteca_emprestimo (tenant_id, coalesce(exemplar_id, (0)::bigint))
    where is_deleted = false
      and ativo = true
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

create unique index if not exists ux_biblioteca_reserva_tenant_exemplar_vigencia
    on sigov.educacao_biblioteca_reserva (tenant_id, coalesce(exemplar_id, (0)::bigint))
    where is_deleted = false
      and ativo = true
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Pos-condicao explicita (regra 13): sem os indices, falha nomeada.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_biblioteca_emprestimo_tenant_exemplar_vigencia') then
        raise exception 'S7-POSTCOND: indice ux_biblioteca_emprestimo_tenant_exemplar_vigencia ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_biblioteca_reserva_tenant_exemplar_vigencia') then
        raise exception 'S7-POSTCOND: indice ux_biblioteca_reserva_tenant_exemplar_vigencia ausente';
    end if;
end $post$;
