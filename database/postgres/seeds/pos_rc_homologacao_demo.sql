-- SIGOV PLUS Pós-RC 27A: seed seguro, idempotente e compatível com o schema final.
-- Não cria schema/tabelas, não usa IDs fixos e não cria usuários/senhas.
\set ON_ERROR_STOP on

insert into sigov.tenant (nome, slug, documento, status, ativo, metadados)
values ('Prefeitura Demo SIGOV','prefeitura-demo-sigov','00.000.000/0000-00','ATIVO',true,'{"ambiente":"Homologacao","dados":"ficticios"}'::jsonb)
on conflict (slug) do update set nome=excluded.nome, documento=excluded.documento, status=excluded.status, ativo=true, metadados=excluded.metadados;

with permissoes_demo(modulo,chave,descricao) as (values
  ('protocolo','protocolo.visualizar','Permissão demo homologação: visualizar protocolo'),
  ('protocolo','protocolo.criar','Permissão demo homologação: criar protocolo'),
  ('protocolo','protocolo.tramitar','Permissão demo homologação: tramitar protocolo'),
  ('ged','ged.visualizar','Permissão demo homologação: visualizar GED'),
  ('ged','ged.upload','Permissão demo homologação: upload GED'),
  ('workflow','workflow.visualizar','Permissão demo homologação: visualizar workflow'),
  ('integracoes','api_key.gerenciar','Permissão demo homologação: gerenciar API key'),
  ('bi','relatorio.exportar','Permissão demo homologação: exportar relatório')
)
insert into sigov.permissao (modulo,chave,descricao,ativo)
select modulo,chave,descricao,true from permissoes_demo
on conflict (modulo,chave) do update set descricao=excluded.descricao, ativo=true;

with tenant_demo as (
  select id from sigov.tenant where slug='prefeitura-demo-sigov'
)
insert into sigov.protocolo (tenant_id,numero,codigo,status,assunto,dados_json,exercicio)
select t.id,v.numero,v.codigo,v.status,v.assunto,v.dados_json::jsonb,2026
from tenant_demo t
cross join (values
  ('2026-000001','DEMO-PROT-001','ABERTO','Solicitação demonstrativa de alvará simplificado','{"interessado":"Pessoa Demo A","setorAtual":"Protocolo"}'),
  ('2026-000002','DEMO-PROT-002','EM_TRAMITACAO','Revisão demonstrativa de cadastro municipal','{"interessado":"Pessoa Demo B","setorAtual":"Tributário"}')
) as v(numero,codigo,status,assunto,dados_json)
where not exists (select 1 from sigov.protocolo p where p.codigo=v.codigo);

with tenant_demo as (select id from sigov.tenant where slug='prefeitura-demo-sigov')
insert into sigov.documento (tenant_id,codigo,status,titulo,hash_sha256,storage_path,classificacao_lgpd,dados_json)
select t.id,v.codigo,'VALIDO',v.titulo,v.hash_sha256,v.storage_path,'PUBLICO',v.dados_json::jsonb
from tenant_demo t
cross join (values
  ('DEMO-DOC-PUB-001','Certidão pública demonstrativa','8f14e45fceea167a5a36dedd4bea2543c9f8b9f1f2f6b1d7b7a7f7f7f7f7f701','demo/publico/certidao-publica-001.txt','{"codigoValidacao":"PUB-DEMO-001"}')
) as v(codigo,titulo,hash_sha256,storage_path,dados_json)
where not exists (select 1 from sigov.documento d where d.codigo=v.codigo);

with tenant_demo as (select id from sigov.tenant where slug='prefeitura-demo-sigov')
insert into sigov.workflow (tenant_id,codigo,status,dados_json)
select id,'DEMO-WF-PROTOCOLO','ATIVO','{"nome":"Fluxo demo Protocolo-GED"}'::jsonb from tenant_demo
where not exists (select 1 from sigov.workflow w where w.codigo='DEMO-WF-PROTOCOLO');

with tenant_demo as (select id from sigov.tenant where slug='prefeitura-demo-sigov')
insert into sigov.api_key (tenant_id,nome,prefixo,api_key_hash,algoritmo_hash,status,dados_json)
select id,'API Key Demo Homologação','sigov_demo','fc86ee2b04157910a83296966cd5033de0f564cbe8dc64d1f3a54238fb32063a','SHA-256','ATIVA','{"aviso":"token claro nunca é salvo no banco","producao":false}'::jsonb from tenant_demo
where not exists (select 1 from sigov.api_key k where k.prefixo='sigov_demo');

with tenant_demo as (select id from sigov.tenant where slug='prefeitura-demo-sigov')
insert into sigov.outbox_evento (tenant_id,event_id,event_type,aggregate_type,aggregate_id,payload,status,idempotency_key)
select t.id, gen_random_uuid(), v.event_type, v.aggregate_type, v.aggregate_id, v.payload::jsonb, 'PENDING', v.idempotency_key
from tenant_demo t
cross join (values
  ('protocolo.criado','protocolo','DEMO-PROT-001','{"codigo":"DEMO-PROT-001"}','seed:protocolo:DEMO-PROT-001'),
  ('documento.criado','documento','DEMO-DOC-PUB-001','{"codigo":"DEMO-DOC-PUB-001"}','seed:documento:DEMO-DOC-PUB-001')
) as v(event_type,aggregate_type,aggregate_id,payload,idempotency_key)
on conflict (idempotency_key) do update set payload=excluded.payload, status=excluded.status;
