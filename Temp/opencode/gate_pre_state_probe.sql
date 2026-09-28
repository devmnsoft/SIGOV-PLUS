\pset footer off
select 'LEDGER' as s, version, success, checksum from sigov.schema_migrations where version like '2026%' order by version;
select 'COLS' as s, column_name, coalesce(character_maximum_length::text,''), is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' order by ordinal_position;
select 'CONSTRAINTS' as s, conname, pg_get_constraintdef(oid) def from pg_constraint where conrelid='sigov.compras_empresarial_recebimento_divergencia'::regclass order by conname;
select 'ROWCOUNT' as s, count(*)::text v1, min(situacao) v2, max(situacao) v3 from sigov.compras_empresarial_recebimento_divergencia;
select 'ROW_DETAIL' as s, id::text, situacao, left(coalesce(resultado,'<null>'),60), left(coalesce(justificativa_encerramento,'<null>'),60), coalesce(encerrada_em::text,'') from sigov.compras_empresarial_recebimento_divergencia order by id limit 30;
select 'APROV_COL' as s, column_name from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name in ('ciclo','aprovador_id') order by 2;
select 'APROV_CONSTRAINTS' as s, conname, pg_get_constraintdef(oid) def from pg_constraint where conrelid='sigov.compras_empresarial_aprovacao'::regclass and contype='u' order by conname;
select 'POLITICA_EXISTS' as s, count(*)::text from information_schema.tables where table_schema='sigov' and table_name in ('compras_empresarial_aprovacao_politica','compras_empresarial_aprovacao_politica_nivel');
select 'IDEM_HASH' as s, count(*)::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_idempotencia' and column_name='request_hash';
select 'PERM_MODULOS' as s, modulo, count(*)::text from sigov.permissao where lower(modulo)='compras_empresariais' group by modulo order by 2;
