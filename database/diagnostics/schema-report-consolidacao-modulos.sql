-- SIGOV PLUS - schema report de consolidação funcional
-- Consulta metadados sem alterar dados.
\pset format aligned
\pset pager off

\echo '== Tabelas sigov =='
select table_schema, table_name, table_type
from information_schema.tables
where table_schema = 'sigov'
order by table_name;

\echo '== Colunas sigov =='
select table_name, ordinal_position, column_name, data_type, is_nullable, column_default
from information_schema.columns
where table_schema = 'sigov'
order by table_name, ordinal_position;

\echo '== Constraints =='
select tc.table_name, tc.constraint_name, tc.constraint_type, kcu.column_name
from information_schema.table_constraints tc
left join information_schema.key_column_usage kcu
  on kcu.constraint_schema = tc.constraint_schema and kcu.constraint_name = tc.constraint_name
where tc.table_schema = 'sigov'
order by tc.table_name, tc.constraint_name, kcu.ordinal_position;

\echo '== Indices =='
select schemaname, tablename, indexname, indexdef
from pg_indexes
where schemaname = 'sigov'
order by tablename, indexname;

\echo '== Tabelas com/sem colunas transversais =='
select t.table_name,
       exists(select 1 from information_schema.columns c where c.table_schema='sigov' and c.table_name=t.table_name and c.column_name='tenant_id') as has_tenant_id,
       exists(select 1 from information_schema.columns c where c.table_schema='sigov' and c.table_name=t.table_name and c.column_name='created_at') as has_created_at,
       exists(select 1 from information_schema.columns c where c.table_schema='sigov' and c.table_name=t.table_name and c.column_name='updated_at') as has_updated_at,
       exists(select 1 from information_schema.columns c where c.table_schema='sigov' and c.table_name=t.table_name and c.column_name='is_deleted') as has_is_deleted,
       exists(select 1 from pg_indexes i where i.schemaname='sigov' and i.tablename=t.table_name) as has_index
from information_schema.tables t
where t.table_schema='sigov' and t.table_type='BASE TABLE'
order by t.table_name;

\echo '== Classificação operacional aproximada =='
select table_name,
       case
         when table_name like '%audit%' or table_name like '%auditoria%' then 'auditoria'
         when table_name like 'tenant%' or table_name like 'plano%' or table_name like 'assinatura%' then 'saas'
         when table_name in ('workflow','workflow_etapa','workflow_instancia','workflow_historico','tarefa','notificacao','notificacao_usuario','agenda_prazo','evento_operacional','outbox_evento') then 'transversal-operacional'
         when table_name in ('protocolo','documento','ged_pasta','arquivo','contrato','compra_solicitacao','licitacao','patrimonio_bem','obra') then 'modulo-critico'
         else 'setorial/operacional'
       end as classificacao
from information_schema.tables
where table_schema='sigov' and table_type='BASE TABLE'
order by classificacao, table_name;
