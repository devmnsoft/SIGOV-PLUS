-- SIGOV - Integrações oficiais, outbox avançado, webhooks, API credentials e estruturas Gov.br/ICP-Brasil.
-- Migration incremental e idempotente; todas as tabelas permanecem no schema único sigov.

alter table sigov.fila_evento add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table sigov.fila_evento add column if not exists entidade_id bigint null references sigov.entidade(id);
alter table sigov.fila_evento add column if not exists exercicio_id bigint null references sigov.exercicio(id);
alter table sigov.fila_evento add column if not exists headers jsonb not null default '{}'::jsonb;
alter table sigov.fila_evento add column if not exists prioridade int not null default 5;
alter table sigov.fila_evento add column if not exists max_tentativas int not null default 5;
alter table sigov.fila_evento add column if not exists proxima_tentativa_at timestamptz null;
alter table sigov.fila_evento add column if not exists erro text null;
alter table sigov.fila_evento add column if not exists dead_letter boolean not null default false;
alter table sigov.fila_evento add column if not exists origem varchar(100) null;
alter table sigov.fila_evento add column if not exists destino varchar(100) null;
alter table sigov.fila_evento add column if not exists idempotency_key varchar(150) null;
alter table sigov.fila_evento add column if not exists updated_at timestamptz null;
update sigov.fila_evento set payload = '{}'::jsonb where payload is null;
alter table sigov.fila_evento alter column payload set default '{}'::jsonb;
alter table sigov.fila_evento alter column status type varchar(40);

alter table sigov.api_credential add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table sigov.api_credential alter column api_key_hash type varchar(500);
alter table sigov.api_credential add column if not exists descricao text null;
alter table sigov.api_credential add column if not exists client_id varchar(120) null;
alter table sigov.api_credential add column if not exists api_key_prefix varchar(40) null;
alter table sigov.api_credential add column if not exists secret_hash varchar(500) null;
alter table sigov.api_credential add column if not exists algoritmo_hash varchar(80) not null default 'PBKDF2-SHA256-100000';
alter table sigov.api_credential add column if not exists scopes jsonb not null default '[]'::jsonb;
alter table sigov.api_credential add column if not exists permitido_ips jsonb not null default '[]'::jsonb;
alter table sigov.api_credential add column if not exists last_used_at timestamptz null;
alter table sigov.api_credential add column if not exists status varchar(40) not null default 'ATIVA';
update sigov.api_credential set client_id = coalesce(client_id, codigo_externo, 'legacy-' || id::text), api_key_prefix = coalesce(api_key_prefix, left(md5(id::text), 12)), scopes = coalesce(scopes, escopos, '[]'::jsonb) where client_id is null or api_key_prefix is null;
alter table sigov.api_credential alter column client_id set not null;
alter table sigov.api_credential alter column api_key_prefix set not null;

do $$ begin
    if not exists (select 1 from pg_constraint where conname = 'uk_sigov_api_credential_tenant_client') then
        alter table sigov.api_credential add constraint uk_sigov_api_credential_tenant_client unique (tenant_id, client_id);
    end if;
end $$;

create table if not exists sigov.api_credential_scope (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    api_credential_id bigint not null references sigov.api_credential(id),
    scope varchar(150) not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, api_credential_id, scope)
);

alter table sigov.integracao_sistema add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table sigov.integracao_sistema alter column nome type varchar(250);
alter table sigov.integracao_sistema alter column tipo drop not null;
alter table sigov.integracao_sistema add column if not exists codigo varchar(100) null;
alter table sigov.integracao_sistema add column if not exists tipo_integracao varchar(80) null;
alter table sigov.integracao_sistema add column if not exists ambiente varchar(40) not null default 'DEVELOPMENT';
alter table sigov.integracao_sistema add column if not exists base_url varchar(500) null;
alter table sigov.integracao_sistema add column if not exists status varchar(40) not null default 'ATIVA';
alter table sigov.integracao_sistema add column if not exists segredo_configurado boolean not null default false;
update sigov.integracao_sistema set codigo = coalesce(codigo, codigo_externo, upper(regexp_replace(nome, '[^a-zA-Z0-9]+', '_', 'g'))), tipo_integracao = coalesce(tipo_integracao, tipo, 'OUTROS'), configuracao = coalesce(configuracao, '{}'::jsonb) where codigo is null or tipo_integracao is null or configuracao is null;
alter table sigov.integracao_sistema alter column codigo set not null;
alter table sigov.integracao_sistema alter column tipo_integracao set not null;
alter table sigov.integracao_sistema alter column configuracao set default '{}'::jsonb;
alter table sigov.integracao_sistema alter column configuracao set not null;

do $$ begin if not exists (select 1 from pg_constraint where conname='uk_sigov_integracao_sistema_tenant_codigo') then alter table sigov.integracao_sistema add constraint uk_sigov_integracao_sistema_tenant_codigo unique (tenant_id, codigo); end if; end $$;

create table if not exists sigov.integracao_endpoint (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), integracao_sistema_id bigint not null references sigov.integracao_sistema(id), nome varchar(150) not null, metodo varchar(20) not null, path varchar(500) not null, timeout_segundos int not null default 30, retry_habilitado boolean not null default true,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

alter table sigov.webhook_recebido add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table sigov.webhook_recebido add column if not exists origem varchar(150) null;
alter table sigov.webhook_recebido add column if not exists evento varchar(150) null;
alter table sigov.webhook_recebido add column if not exists assinatura varchar(500) null;
alter table sigov.webhook_recebido add column if not exists assinatura_valida boolean null;
alter table sigov.webhook_recebido add column if not exists idempotency_key varchar(150) null;
alter table sigov.webhook_recebido add column if not exists status varchar(40) not null default 'RECEBIDO';
alter table sigov.webhook_recebido add column if not exists processado_at timestamptz null;
alter table sigov.webhook_recebido add column if not exists erro text null;
alter table sigov.webhook_recebido add column if not exists ip varchar(80) null;
alter table sigov.webhook_recebido add column if not exists user_agent text null;
update sigov.webhook_recebido set origem = coalesce(origem, codigo_externo, 'legacy'), evento = coalesce(evento, 'WebhookRecebido'), headers = coalesce(headers, '{}'::jsonb), payload = coalesce(payload, '{}'::jsonb), status = case when processado then 'PROCESSADO' else status end where origem is null or evento is null or headers is null or payload is null;
alter table sigov.webhook_recebido alter column origem set not null;
alter table sigov.webhook_recebido alter column evento set not null;
alter table sigov.webhook_recebido alter column headers set default '{}'::jsonb;
alter table sigov.webhook_recebido alter column headers set not null;

create table if not exists sigov.webhook_enviado (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), integracao_sistema_id bigint null references sigov.integracao_sistema(id), destino varchar(150) not null, url varchar(500) not null, evento varchar(150) not null, headers jsonb not null default '{}'::jsonb, payload jsonb not null default '{}'::jsonb, status varchar(40) not null default 'PENDENTE', tentativas int not null default 0, proxima_tentativa_at timestamptz null, enviado_at timestamptz null, resposta_status int null, resposta_body text null, erro text null, created_at timestamptz not null default now(), correlation_id uuid null
);
create table if not exists sigov.webhook_assinatura (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), nome varchar(150) not null, algoritmo varchar(80) not null default 'HMAC-SHA256', secret_hash varchar(500) not null, header_nome varchar(100) not null default 'X-Sigov-Signature', ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.idempotency_key (
    id bigint generated always as identity primary key, tenant_id bigint null references sigov.tenant(id), chave varchar(150) not null, metodo varchar(20) not null, rota varchar(500) not null, request_hash varchar(128) not null, response_hash varchar(128) null, status varchar(40) not null, expires_at timestamptz not null, created_at timestamptz not null default now(), unique(tenant_id, chave)
);
create table if not exists sigov.integracao_log (
    id bigint generated always as identity primary key, tenant_id bigint null references sigov.tenant(id), integracao_sistema_id bigint null references sigov.integracao_sistema(id), direcao varchar(20) not null, tipo_evento varchar(150) not null, status varchar(40) not null, request_resumo jsonb null, response_resumo jsonb null, duracao_ms bigint null, correlation_id uuid null, created_at timestamptz not null default now()
);
create table if not exists sigov.integracao_erro (
    id bigint generated always as identity primary key, tenant_id bigint null references sigov.tenant(id), integracao_sistema_id bigint null references sigov.integracao_sistema(id), tipo_erro varchar(120) not null, mensagem text not null, detalhe jsonb not null default '{}'::jsonb, tratado boolean not null default false, created_at timestamptz not null default now(), correlation_id uuid null
);
create table if not exists sigov.integracao_job_execucao (
    id bigint generated always as identity primary key, tenant_id bigint null references sigov.tenant(id), job_nome varchar(150) not null, status varchar(40) not null, inicio_at timestamptz not null default now(), fim_at timestamptz null, itens_processados int not null default 0, erro text null, correlation_id uuid null
);
create table if not exists sigov.govbr_configuracao (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), ambiente varchar(40) not null, client_id varchar(150) null, client_secret_hash varchar(500) null, redirect_uri varchar(500) null, configuracao jsonb not null default '{}'::jsonb, ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.certificado_digital (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), nome varchar(150) not null, tipo_certificado varchar(40) not null, subject varchar(500) null, issuer varchar(500) null, serial_number varchar(200) null, validade_inicio date null, validade_fim date null, storage_key varchar(500) null, thumbprint varchar(200) null, status varchar(40) not null default 'ATIVO', metadados jsonb not null default '{}'::jsonb, ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.assinador_digital (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), nome varchar(150) not null, certificado_digital_id bigint null references sigov.certificado_digital(id), modo varchar(40) not null default 'ESTRUTURAL', configuracao jsonb not null default '{}'::jsonb, ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.remessa_oficial (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), entidade_id bigint null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), tipo_remessa varchar(80) not null, competencia varchar(20) null, numero varchar(80) not null, status varchar(40) not null, gerado_at timestamptz null, enviado_at timestamptz null, protocolo_externo varchar(150) null, payload jsonb not null default '{}'::jsonb, retorno jsonb not null default '{}'::jsonb, erro text null, ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.remessa_oficial_item (
    id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), remessa_oficial_id bigint not null references sigov.remessa_oficial(id), tipo_item varchar(80) not null, payload jsonb not null default '{}'::jsonb, status varchar(40) not null default 'PENDENTE', erro text null, ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.integracao_evento (
    id bigint generated always as identity primary key, tenant_id bigint null references sigov.tenant(id), tipo_evento varchar(150) not null, payload jsonb not null default '{}'::jsonb, status varchar(40) not null default 'REGISTRADO', created_at timestamptz not null default now(), correlation_id uuid null
);

create index if not exists idx_fila_evento_tenant_status on sigov.fila_evento (tenant_id, status);
create index if not exists idx_fila_evento_proxima_tentativa on sigov.fila_evento (proxima_tentativa_at) where status in ('PENDENTE','ERRO');
create index if not exists idx_fila_evento_dead_letter on sigov.fila_evento (tenant_id, dead_letter);
create index if not exists idx_api_credential_tenant_client on sigov.api_credential (tenant_id, client_id);
create index if not exists idx_integracao_sistema_tenant_codigo on sigov.integracao_sistema (tenant_id, codigo);
create index if not exists idx_webhook_recebido_tenant_status on sigov.webhook_recebido (tenant_id, status);
create index if not exists idx_webhook_recebido_idempotency on sigov.webhook_recebido (tenant_id, idempotency_key);
create index if not exists idx_webhook_enviado_tenant_status on sigov.webhook_enviado (tenant_id, status);
create index if not exists idx_idempotency_tenant_chave on sigov.idempotency_key (tenant_id, chave);
create index if not exists idx_integracao_log_tenant_created on sigov.integracao_log (tenant_id, created_at desc);
create index if not exists idx_remessa_oficial_tenant_tipo on sigov.remessa_oficial (tenant_id, tipo_remessa);
create index if not exists idx_certificado_tenant_status on sigov.certificado_digital (tenant_id, status);

create or replace view sigov.vw_integracao_dashboard as
select t.id as tenant_id,
       (select count(1) from sigov.integracao_sistema s where s.tenant_id=t.id and s.is_deleted=false) total_sistemas,
       (select count(1) from sigov.fila_evento f where f.tenant_id=t.id and f.status='PENDENTE') outbox_pendentes,
       (select count(1) from sigov.webhook_recebido w where w.tenant_id=t.id and w.created_at::date=current_date) webhooks_recebidos_hoje,
       (select count(1) from sigov.remessa_oficial r where r.tenant_id=t.id and r.is_deleted=false) remessas_total
from sigov.tenant t;
create or replace view sigov.vw_integracao_outbox_resumo as select tenant_id,status,dead_letter,count(1) total from sigov.fila_evento group by tenant_id,status,dead_letter;
create or replace view sigov.vw_integracao_webhooks_resumo as select tenant_id,status,count(1) total from sigov.webhook_recebido group by tenant_id,status;
create or replace view sigov.vw_integracao_remessas_resumo as select tenant_id,tipo_remessa,status,count(1) total from sigov.remessa_oficial group by tenant_id,tipo_remessa,status;
create or replace view sigov.vw_integracao_erros_recentes as select tenant_id,tipo_erro,mensagem,created_at,correlation_id from sigov.integracao_erro order by created_at desc limit 100;

insert into sigov.modulo_saas (codigo,nome,descricao,categoria,ordem,rota_base,icone,ativo)
values ('integracao','Integrações','Integrações oficiais, API credentials, webhooks, outbox, Gov.br e ICP-Brasil estrutural.','Plataforma',90,'/Integracoes','bi-diagram-3',true)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, ativo=true;

insert into sigov.permissao (modulo,chave,recurso,acao,descricao,ativo) values
('integracao','integracao.api_credential.visualizar','integracao.api_credential','visualizar','Visualizar API credentials',true),
('integracao','integracao.api_credential.criar','integracao.api_credential','criar','Criar API credentials',true),
('integracao','integracao.api_credential.revogar','integracao.api_credential','revogar','Revogar API credentials',true),
('integracao','integracao.sistema.visualizar','integracao.sistema','visualizar','Visualizar sistemas de integração',true),
('integracao','integracao.sistema.criar','integracao.sistema','criar','Criar sistemas de integração',true),
('integracao','integracao.sistema.editar','integracao.sistema','editar','Editar sistemas de integração',true),
('integracao','integracao.sistema.excluir','integracao.sistema','excluir','Excluir sistemas de integração',true),
('integracao','integracao.sistema.testar','integracao.sistema','testar','Testar adapters dev',true),
('integracao','integracao.webhook.visualizar','integracao.webhook','visualizar','Visualizar webhooks',true),
('integracao','integracao.webhook.receber','integracao.webhook','receber','Receber webhooks',true),
('integracao','integracao.webhook.enviar','integracao.webhook','enviar','Enviar webhooks dev',true),
('integracao','integracao.webhook.reprocessar','integracao.webhook','reprocessar','Reprocessar webhooks',true),
('integracao','integracao.outbox.visualizar','integracao.outbox','visualizar','Visualizar outbox',true),
('integracao','integracao.outbox.criar','integracao.outbox','criar','Criar outbox',true),
('integracao','integracao.outbox.reprocessar','integracao.outbox','reprocessar','Reprocessar outbox',true),
('integracao','integracao.outbox.dead_letter','integracao.outbox','dead_letter','Mover para dead-letter',true),
('integracao','integracao.remessa.visualizar','integracao.remessa','visualizar','Visualizar remessas',true),
('integracao','integracao.remessa.criar','integracao.remessa','criar','Criar remessas',true),
('integracao','integracao.remessa.gerar','integracao.remessa','gerar','Gerar remessas dev',true),
('integracao','integracao.remessa.enviar','integracao.remessa','enviar','Enviar remessas dev',true),
('integracao','integracao.remessa.cancelar','integracao.remessa','cancelar','Cancelar remessas',true),
('integracao','integracao.certificado.visualizar','integracao.certificado','visualizar','Visualizar certificados',true),
('integracao','integracao.certificado.criar','integracao.certificado','criar','Criar certificados',true),
('integracao','integracao.certificado.revogar','integracao.certificado','revogar','Revogar certificados',true),
('integracao','integracao.govbr.configurar','integracao.govbr','configurar','Configurar Gov.br estrutural',true),
('integracao','integracao.assinador.usar','integracao.assinador','usar','Usar assinador estrutural',true),
('integracao','integracao.dashboard.visualizar','integracao.dashboard','visualizar','Visualizar dashboard de integrações',true),
('integracao','integracao.exportar','integracao.exportacao','exportar','Exportar dados de integrações',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.integracao_sistema (tenant_id, codigo, nome, tipo_integracao, ambiente, status, configuracao, segredo_configurado, ativo)
select t.id, v.codigo, v.nome, v.tipo_integracao, 'DEVELOPMENT', 'CONFIGURACAO_PENDENTE', '{}'::jsonb, false, true
from sigov.tenant t
cross join (values
 ('GOVBR','Gov.br Estrutural','GOVBR'),('ICP_BRASIL','ICP-Brasil Estrutural','ICP_BRASIL'),('TCE','TCE Estrutural','TCE'),('ESOCIAL','eSocial Estrutural','ESOCIAL'),('EDUCACENSO','Educacenso Estrutural','EDUCACENSO'),('ESUS','e-SUS Estrutural','ESUS'),('ABRASF_NFSE','ABRASF/NFS-e Estrutural','ABRASF_NFSE'),('DESIF','DES-IF Estrutural','DESIF'),('BANCO','Bancos/Arquivos Estrutural','BANCO'),('PIX','PIX Estrutural','PIX'),('WEBHOOK','Webhooks Estruturais','WEBHOOK')
) as v(codigo,nome,tipo_integracao)
where t.slug = 'municipio-demo'
on conflict (tenant_id, codigo) do nothing;
