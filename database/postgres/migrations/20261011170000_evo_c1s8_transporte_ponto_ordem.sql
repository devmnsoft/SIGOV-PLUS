-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 8 (educacao/transporte_escolar): ordem unica por rota
-- para os pontos de embarque/desembarque (somente vigentes) e reaproveitamento
-- da unicidade aluno x rota x turno (rc50_48) como backstop transacional dos
-- comandos tipados de rota/ponto/aluno. Regra 24: nada municipal hardcoded —
-- a chave e tenant x rota, valida para qualquer esfera/orgao. Estados terminais
-- (ENCERRADA/ENCERRADO/CANCELADA/CANCELADO) liberam a ordem, como nas politicas
-- de unicidade das Sprints 3, 5, 6 e 7. Os comandos tipados
-- (POST /api/educacao/transporte/rotas/lancar,
-- POST /api/educacao/transporte/rotas/{id}/pontos/lancar e
-- POST /api/educacao/transporte/rotas/{id}/alunos/vincular) checam antes de
-- gravar (falhas nomeadas 409); os indices sao o backstop transacional.
-- Idempotente; multi-esfera (regra 24).
-- ============================================================================

create unique index if not exists ux_transporte_ponto_tenant_rota_ordem
    on sigov.educacao_transporte_rota_ponto (tenant_id, coalesce(rota_id, (0)::bigint), coalesce(numero, (0)::bigint))
    where is_deleted = false
      and ativo = true
      and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO');

-- Pos-condicao explicita (regra 13): sem os indices, falha nomeada. O segundo
-- indice pertence ao rc50_48 (unicao aluno x rota x turno) e e verificado aqui
-- porque e o backstop do comando de vinculacao de aluno.
do $post$
begin
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_transporte_ponto_tenant_rota_ordem') then
        raise exception 'S8-POSTCOND: indice ux_transporte_ponto_tenant_rota_ordem ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_educacao_transporte_aluno_rota_turno') then
        raise exception 'S8-POSTCOND: indice ux_educacao_transporte_aluno_rota_turno ausente';
    end if;
end $post$;
