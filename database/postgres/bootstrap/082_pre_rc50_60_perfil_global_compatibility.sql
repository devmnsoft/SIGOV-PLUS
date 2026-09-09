-- Compatibilidade forward-only para os templates globais da RC50.60.
-- A migration 015 tornou tenant_id obrigatório em tabelas operacionais,
-- inclusive perfil_acesso. A RC50.60 publicada introduz perfis funcionais
-- globais e omite tenant_id deliberadamente; as concessões continuam
-- vinculadas ao tenant em perfil_permissao/grupo_perfil.
alter table if exists sigov.perfil_acesso
    alter column tenant_id drop not null;
