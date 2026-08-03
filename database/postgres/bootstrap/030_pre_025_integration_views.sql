-- SIGOV+ RC38E
-- A migration 025 altera tipos de colunas usadas pelas próprias views que ela cria.
-- Na segunda execução, PostgreSQL impede ALTER TYPE enquanto essas views existem.
-- Removê-las imediatamente antes é seguro porque a mesma migration as recria.

drop view if exists sigov.vw_integracao_dashboard;
drop view if exists sigov.vw_integracao_outbox_resumo;
drop view if exists sigov.vw_integracao_webhooks_resumo;
drop view if exists sigov.vw_integracao_remessas_resumo;
drop view if exists sigov.vw_integracao_erros_recentes;
