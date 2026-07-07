-- SIGOV PLUS Pós-RC 04: seed demonstrável de homologação.
-- Seguro, fictício, idempotente e destinado apenas a Development/Homologation.
\set ON_ERROR_STOP on

create schema if not exists sigov;

-- Tabelas transversais mínimas quando o ambiente recebeu somente migrations Pós-RC.
create table if not exists sigov.tenant (id bigserial primary key, nome varchar(200) not null, slug varchar(120), documento varchar(30), email varchar(180), status varchar(30) default 'ATIVO', ativo boolean default true, dados_json jsonb, created_at timestamptz default now(), is_deleted boolean default false);
create table if not exists sigov.usuario (id bigserial primary key, tenant_id bigint, nome varchar(200), login varchar(120), email varchar(180), senha_hash text, ativo boolean default true, dados_json jsonb, created_at timestamptz default now(), is_deleted boolean default false);
create table if not exists sigov.permissao (id bigserial primary key, codigo varchar(120) not null unique, descricao varchar(240), created_at timestamptz default now());
create table if not exists sigov.usuario_permissao (id bigserial primary key, tenant_id bigint, usuario_id bigint, permissao_id bigint, created_at timestamptz default now(), unique(usuario_id, permissao_id));

insert into sigov.tenant (id,nome,slug,documento,email,status,ativo,dados_json)
values (1,'Prefeitura Demo SIGOV','prefeitura-demo-sigov','00.000.000/0000-00','homologacao-demo@sigov.local','ATIVO',true,'{"ambiente":"Homologacao","dados":"ficticios"}'::jsonb)
on conflict (id) do update set nome=excluded.nome, slug=excluded.slug, documento=excluded.documento, status='ATIVO', ativo=true, dados_json=excluded.dados_json;

insert into sigov.usuario (id,tenant_id,nome,login,email,senha_hash,ativo,dados_json) values
(1,1,'Admin Geral Demo','admin.geral','admin.geral@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"admin geral","senha_demo":"documentada fora do banco"}'::jsonb),
(2,1,'Admin Tenant Demo','admin.tenant','admin.tenant@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"admin tenant"}'::jsonb),
(3,1,'Coordenação Protocolo Demo','coord.protocolo','coord.protocolo@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"coordenador protocolo"}'::jsonb),
(4,1,'Servidor Protocolo Demo','servidor.protocolo','servidor.protocolo@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"servidor protocolo"}'::jsonb),
(5,1,'Operador GED Demo','operador.ged','operador.ged@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"operador ged"}'::jsonb),
(6,1,'Consulta Demo','consulta','consulta@sigov.local','DEMO_HASH_NAO_USAR_EM_PRODUCAO',true,'{"perfil":"consulta"}'::jsonb)
on conflict (id) do update set tenant_id=excluded.tenant_id,nome=excluded.nome,login=excluded.login,email=excluded.email,ativo=true,dados_json=excluded.dados_json;

insert into sigov.permissao (codigo,descricao) select p, 'Permissão demo Pós-RC 04' from unnest(array[
'protocolo.visualizar','protocolo.criar','protocolo.tramitar','protocolo.anexar','ged.visualizar','ged.upload','ged.download','workflow.visualizar','workflow.avancar','tarefa.visualizar','tarefa.concluir','notificacao.visualizar','api_key.gerenciar','webhook.gerenciar','relatorio.exportar'
]) p on conflict (codigo) do nothing;
insert into sigov.usuario_permissao (tenant_id,usuario_id,permissao_id)
select 1,u.id,p.id from sigov.usuario u cross join sigov.permissao p where u.tenant_id=1 on conflict do nothing;

-- Dados operacionais reais no schema Pós-RC. Todos os documentos/pessoas são fictícios e mascaráveis.
insert into sigov.protocolo (tenant_id,numero,codigo,status,assunto,dados_json,created_by,created_at,exercicio) values
(1,'2026-000001','DEMO-PROT-001','ABERTO','Solicitação demonstrativa de alvará simplificado','{"interessado":"Pessoa Demo A","interessadoDocumento":"00000000000","interessadoEmail":"pessoa.a@demo.local","setorAtual":"Protocolo"}',3,now()-interval '5 days',2026),
(1,'2026-000002','DEMO-PROT-002','EM_TRAMITACAO','Revisão demonstrativa de cadastro municipal','{"interessado":"Pessoa Demo B","interessadoDocumento":"11111111111","setorAtual":"Tributário"}',3,now()-interval '4 days',2026),
(1,'2026-000003','DEMO-PROT-003','CONCLUIDO','Entrega demonstrativa de certidão pública','{"interessado":"Pessoa Demo C","interessadoDocumento":"22222222222","setorAtual":"Arquivo"}',4,now()-interval '3 days',2026),
(1,'2026-000004','DEMO-PROT-004','PENDENTE','Análise demonstrativa de documento restrito','{"interessado":"Pessoa Demo D","interessadoDocumento":"33333333333","setorAtual":"GED"}',5,now()-interval '2 days',2026),
(1,'2026-000005','DEMO-PROT-005','SUSPENSO','Pendência demonstrativa para complementação','{"interessado":"Pessoa Demo E","interessadoDocumento":"44444444444","setorAtual":"Protocolo"}',4,now()-interval '1 day',2026)
on conflict do nothing;

insert into sigov.protocolo_movimento (tenant_id,protocolo_id,status,observacao,created_by,created_at)
select 1,p.id,'TRAMITADO','Movimento demo auditável sem dado sensível completo.',3,p.created_at+interval '2 hours' from sigov.protocolo p where p.codigo like 'DEMO-PROT-%' and not exists (select 1 from sigov.protocolo_movimento m where m.protocolo_id=p.id);

insert into sigov.documento (tenant_id,codigo,status,titulo,hash_sha256,storage_path,classificacao_lgpd,dados_json,created_by,created_at) values
(1,'DEMO-DOC-PUB-001','VALIDO','Certidão pública demonstrativa','8f14e45fceea167a5a36dedd4bea2543c9f8b9f1f2f6b1d7b7a7f7f7f7f7f701','demo/publico/certidao-publica-001.txt','PUBLICO','{"codigoValidacao":"PUB-DEMO-001","observacao":"Documento público fictício"}',5,now()-interval '3 days'),
(1,'DEMO-DOC-PUB-002','VALIDO','Comprovante público demonstrativo','c9f0f895fb98ab9159f51fd0297e236d11111111111111111111111111111111','demo/publico/comprovante-publico-002.txt','PUBLICO','{"codigoValidacao":"PUB-DEMO-002","observacao":"Documento público fictício"}',5,now()-interval '2 days'),
(1,'DEMO-DOC-RES-001','RESTRITO','Parecer restrito demonstrativo','45c48cce2e2d7fbdea1afc51c7c6ad2611111111111111111111111111111111','demo/restrito/parecer-restrito-001.txt','RESTRITO','{"observacao":"Restrito; não baixar na validação pública"}',5,now()-interval '1 day')
on conflict do nothing;
insert into sigov.documento_versao (tenant_id,documento_id,versao,hash_sha256,storage_path,status,created_by)
select d.tenant_id,d.id,1,d.hash_sha256,d.storage_path,'ATIVA',5 from sigov.documento d where d.codigo like 'DEMO-DOC-%' and not exists (select 1 from sigov.documento_versao v where v.documento_id=d.id);
insert into sigov.protocolo_anexo (tenant_id,protocolo_id,documento_id,status,created_by)
select 1,p.id,d.id,'ATIVO',5 from sigov.protocolo p join sigov.documento d on d.codigo in ('DEMO-DOC-PUB-001','DEMO-DOC-PUB-002','DEMO-DOC-RES-001') where p.codigo='DEMO-PROT-001' on conflict do nothing;

insert into sigov.workflow (tenant_id,codigo,status,dados_json,created_by) values (1,'DEMO-WF-PROTOCOLO','ATIVO','{"nome":"Fluxo demo Protocolo-GED"}',3) on conflict do nothing;
insert into sigov.workflow_instancia (tenant_id,workflow_id,protocolo_id,status,dados_json,created_by)
select 1,(select id from sigov.workflow where codigo='DEMO-WF-PROTOCOLO' limit 1),p.id,'ATIVO','{"etapa":"Triagem"}',3 from sigov.protocolo p where p.codigo like 'DEMO-PROT-%' and not exists (select 1 from sigov.workflow_instancia wi where wi.protocolo_id=p.id);
insert into sigov.tarefa (tenant_id,protocolo_id,workflow_instancia_id,titulo,status,responsavel_id,dados_json,created_by,created_at,concluida_at)
select 1,p.id,wi.id,'Tarefa demo '||p.numero,case when p.status='CONCLUIDO' then 'CONCLUIDA' when p.status='PENDENTE' then 'VENCIDA' else 'PENDENTE' end,4,'{"prazo":"ficticio"}',3,p.created_at+interval '4 hours',case when p.status='CONCLUIDO' then now()-interval '1 day' else null end from sigov.protocolo p left join sigov.workflow_instancia wi on wi.protocolo_id=p.id where p.codigo like 'DEMO-PROT-%' and not exists (select 1 from sigov.tarefa t where t.protocolo_id=p.id);
insert into sigov.notificacao (tenant_id,titulo,mensagem,status,dados_json,created_by,created_at) values
(1,'Demo: protocolo criado','Há protocolo demo aguardando triagem.','NAO_LIDA','{"tipo":"demo"}',3,now()-interval '1 day'),
(1,'Demo: documento publicado','Documento público demo pronto para validação.','LIDA','{"tipo":"demo"}',5,now()-interval '2 days')
on conflict do nothing;

insert into sigov.outbox_evento (tenant_id,evento,agregado,agregado_id,payload,status,tentativas,erro_mascarado,created_by,created_at) values
(1,'protocolo.criado','protocolo',1,'{"numero":"2026-000001","interessado":"Pessoa D***"}','PENDENTE',0,null,3,now()-interval '2 hours'),
(1,'documento.criado','documento',1,'{"codigo":"DEMO-DOC-PUB-001","classificacao":"PUBLICO"}','ENTREGUE',1,null,5,now()-interval '3 hours'),
(1,'webhook.teste','webhook',1,'{"teste":true}','FALHOU',3,'Endpoint demo indisponível',2,now()-interval '4 hours')
on conflict do nothing;
insert into sigov.webhook_configuracao (tenant_id,nome,url,secret_hash,eventos,status,dados_json,created_by)
values (1,'Webhook Demo Inativo','https://example.invalid/sigov-webhook','sha256:demo-secret-hash-nunca-claro','["protocolo.criado","documento.criado"]','INATIVO','{"seguro":true,"demo":true}',2) on conflict do nothing;
insert into sigov.webhook_entrega (tenant_id,webhook_configuracao_id,outbox_evento_id,evento,endpoint,status,http_status,tentativa,erro_mascarado,payload_mascarado,created_by)
select 1,(select id from sigov.webhook_configuracao where nome='Webhook Demo Inativo' limit 1),o.id,o.evento,'https://example.invalid/sigov-webhook',case when o.status='ENTREGUE' then 'ENTREGUE' else 'FALHOU' end,case when o.status='ENTREGUE' then 200 else 503 end,o.tentativas,o.erro_mascarado,o.payload,2 from sigov.outbox_evento o where o.evento in ('documento.criado','webhook.teste') and not exists (select 1 from sigov.webhook_entrega e where e.outbox_evento_id=o.id);
insert into sigov.api_key (tenant_id,nome,prefixo,api_key_hash,algoritmo_hash,status,dados_json,created_by)
values (1,'API Key Demo Homologação','sigov_demo','sha256:demo-hash-sem-token-claro','SHA-256','ATIVA','{"aviso":"token claro exibido somente na criação; este seed armazena apenas hash"}',2) on conflict do nothing;
insert into sigov.api_key_escopo (tenant_id,api_key_id,escopo,status,created_by)
select 1,k.id,e,'ATIVO',2 from sigov.api_key k cross join unnest(array['protocolo.read','protocolo.write','documento.read','tarefa.read']) e where k.prefixo='sigov_demo' on conflict do nothing;
