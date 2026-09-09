-- Compatibilidade forward-only do contrato_fiscal transversal com RC50.85.
-- A relação já existia antes do contrato multi-esfera; o índice publicado usa
-- `ativo` antes da migration corretiva posterior que completa o contrato.
alter table if exists sigov.contrato_fiscal
    add column if not exists ativo boolean not null default true;
