-- Compatibilidade forward-only entre o portal do responsável (RC50.34) e
-- o contrato consolidado de Educação (FUNC05). As tabelas já existiam, então
-- os CREATE TABLE IF NOT EXISTS de FUNC05 não acrescentavam o contexto de
-- entidade nem os campos de publicação consumidos pelo fluxo consolidado.
alter table if exists sigov.educacao_comunicado
    add column if not exists entidade_id bigint,
    add column if not exists publicado_em timestamptz not null default now(),
    add column if not exists ativo boolean not null default true;

alter table if exists sigov.educacao_portal_vinculo
    add column if not exists entidade_id bigint;
