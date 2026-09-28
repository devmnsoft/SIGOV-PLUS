-- GATE CLEAN INSTALL (pos-regeneracao 20260927 tighten): probes pos-instalacao.
\pset footer off
select 'LEN_CODIGO' as s, coalesce(character_maximum_length::text,'') from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo';
select 'CONSTRAINTS' as s, conname from pg_constraint where conrelid='sigov.compras_empresarial_recebimento_divergencia'::regclass and conname in ('ck_comp_recb_div_resultado_codigo','ck_comp_recb_div_codigo_encerrado') order by 2;
select 'APROV_CICLO_COL' as s, count(*)::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='ciclo';
select 'APROV_UQ' as s, conname from pg_constraint where conrelid='sigov.compras_empresarial_aprovacao'::regclass and contype='u' order by 2;
select 'USERS_1_2' as s, count(*)::text from sigov.usuario where id in (1,2);
select 'POLITICA_ROWS' as s, (select count(*) from sigov.compras_empresarial_aprovacao_politica)::text || '/' || (select count(*) from sigov.compras_empresarial_aprovacao_politica_nivel)::text;
select 'DIV_ROWS' as s, count(*)::text from sigov.compras_empresarial_recebimento_divergencia;
select 'MAX_MIG' as s, max(version) from sigov.schema_migrations;
