select exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_aprovacao' and column_name='causa_bloqueio') as col_causa,
 exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_idempotencia' and column_name='resultado') as col_resultado,
 exists(select 1 from pg_constraint where conname='ck_compras_aprovacao_causa') as ck_causa;
