-- GATE UPGRADE PATH: asserts pos-migration 20260927 (rodar com -A -F ' | ').
\pset footer off
select 'LEN_CODIGO' as s, coalesce(character_maximum_length::text,'') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo';
select 'DERIV' as s, recebimento_item_id::text, coalesce(resultado_codigo,'<null>') from sigov.compras_empresarial_recebimento_divergencia where tenant_id='b0000001-0000-4000-8000-000000000099'::uuid order by recebimento_item_id;
select 'NEW_CONSTRAINTS' as s, conname from pg_constraint where conrelid='sigov.compras_empresarial_recebimento_divergencia'::regclass and conname in ('ck_comp_recb_div_resultado_codigo','ck_comp_recb_div_codigo_encerrado') order by 2;
select 'CICLO_COL' as s, count(*)::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='ciclo';
select 'APROVADOR_NULLABLE' as s, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='aprovador_id';
select 'APROV_UQ' as s, conname from pg_constraint where conrelid='sigov.compras_empresarial_aprovacao'::regclass and contype='u' order by 2;
select 'APROV_IDX' as s, count(*)::text from pg_indexes where schemaname='sigov' and indexname='ix_comp_aprovacao_ciclo';
select 'POLITICA_TABLES' as s, count(*)::text from information_schema.tables where table_schema='sigov' and table_name in ('compras_empresarial_aprovacao_politica','compras_empresarial_aprovacao_politica_nivel');
select 'POLITICA_UIDX' as s, count(*)::text from pg_indexes where schemaname='sigov' and indexname='uq_comp_aprovacao_politica_ativa';
select 'IDEM_HASH' as s, data_type||'('||coalesce(character_maximum_length::text,'')||')' from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_idempotencia' and column_name='request_hash';
select 'PERM_UPPER' as s, count(*)::text from sigov.permissao where modulo='COMPRAS_EMPRESARIAIS';
select 'PERM_LOWER' as s, count(*)::text from sigov.permissao where modulo='compras_empresariais';
select 'DIV_COUNT' as s, count(*)::text from sigov.compras_empresarial_recebimento_divergencia where tenant_id='b0000001-0000-4000-8000-000000000099'::uuid;
