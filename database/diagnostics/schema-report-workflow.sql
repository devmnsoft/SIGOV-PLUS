-- Relatório de prontidão do contrato de workflow (uma linha por tabela).
with requisitos(tabela, colunas, indices) as (
 values
 ('workflow_definicao', array['id','tenant_id','nome','modulo','status','versao_atual'], array['ix_workflow_definicao_tenant_status']),
 ('workflow_versao', array['id','tenant_id','workflow_definicao_id','numero','status','conteudo_json'], array[]::text[]),
 ('workflow_etapa', array['id','tenant_id','workflow_definicao_id','nome','tipo','ordem'], array['ix_workflow_etapa_tenant_workflow']),
 ('workflow_transicao', array['id','tenant_id','workflow_definicao_id','de_etapa_id','para_etapa_id','acao'], array[]::text[]),
 ('workflow_instancia', array['id','tenant_id'], array[]::text[]),
 ('workflow_historico', array['id','tenant_id','workflow_instancia_id'], array[]::text[]),
 ('workflow_evento', array['id','tenant_id','workflow_instancia_id','tipo','created_at'], array['ix_workflow_evento_tenant_instancia'])
), avaliacao as (
 select r.*,
        to_regclass('sigov.' || r.tabela) is not null as existe,
        array(select c from unnest(r.colunas) c where not exists (
          select 1 from information_schema.columns x
           where x.table_schema='sigov' and x.table_name=r.tabela and x.column_name=c)) as colunas_ausentes,
        array(select i from unnest(r.indices) i where to_regclass('sigov.' || i) is null) as indices_ausentes
 from requisitos r
)
select tabela as "Tabela", existe as "Existe?", array_to_string(colunas, ', ') as "Colunas obrigatórias",
       array_to_string(indices, ', ') as "Índices obrigatórios",
       case when not existe then 'MISSING_TABLE'
            when cardinality(colunas_ausentes)>0 then 'MISSING_COLUMN'
            when cardinality(indices_ausentes)>0 then 'INDEX_MISSING'
            when tabela='workflow_versao' and exists (
              select 1 from sigov.schema_migrations m
               where m.version='20260809160000' and (not m.success or m.checksum is null)) then 'HISTORY_INCONSISTENT'
            else 'OK' end as "Status",
       concat_ws('; ',
          case when cardinality(colunas_ausentes)>0 then 'Colunas: '||array_to_string(colunas_ausentes, ', ') end,
          case when cardinality(indices_ausentes)>0 then 'Índices: '||array_to_string(indices_ausentes, ', ') end) as "Pendência"
from avaliacao order by tabela;
