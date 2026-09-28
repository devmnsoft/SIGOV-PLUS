-- GATE HEAL DEMO: asserts finais (pos-migration).
\pset footer off
select 'POST_LEN' as s, character_maximum_length::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo';
select 'ROWS' as s, recebimento_item_id::text || ' | ' || coalesce(resultado_codigo,'<null>') || ' | ' || situacao from sigov.compras_empresarial_recebimento_divergencia where tenant_id='b0000001-0000-4000-8000-000000000098'::uuid order by recebimento_item_id;
select 'CONSTRAINTS' as s, conname from pg_constraint where conrelid='sigov.compras_empresarial_recebimento_divergencia'::regclass and conname in ('ck_comp_recb_div_resultado_codigo','ck_comp_recb_div_codigo_encerrado') order by 2;
