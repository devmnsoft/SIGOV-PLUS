-- SIGOV+ RC38E
-- Compatibilidade mínima exigida pelo bootstrap operacional após todas as migrations.

create extension if not exists pgcrypto;

alter table sigov.usuario add column if not exists tenant_id bigint null;
alter table sigov.usuario add column if not exists nome varchar(200) null;
alter table sigov.usuario add column if not exists tipo_usuario varchar(80) null;
alter table sigov.usuario add column if not exists senha_deve_ser_alterada boolean not null default false;
alter table sigov.usuario add column if not exists deve_alterar_senha boolean not null default false;
alter table sigov.usuario add column if not exists bloqueado boolean not null default false;
alter table sigov.usuario add column if not exists tentativas_invalidas integer not null default 0;
alter table sigov.usuario add column if not exists bloqueado_ate timestamptz null;

alter table sigov.entidade add column if not exists tenant_id bigint null;
alter table sigov.exercicio add column if not exists tenant_id bigint null;
alter table sigov.pessoa add column if not exists tenant_id bigint null;
alter table sigov.grupo_acesso add column if not exists tenant_id bigint null;
alter table sigov.perfil_acesso add column if not exists tenant_id bigint null;
alter table sigov.usuario_grupo add column if not exists tenant_id bigint null;
alter table sigov.usuario_grupo add column if not exists is_deleted boolean not null default false;
alter table sigov.grupo_perfil add column if not exists tenant_id bigint null;
alter table sigov.grupo_perfil add column if not exists is_deleted boolean not null default false;
alter table sigov.perfil_permissao add column if not exists tenant_id bigint null;
alter table sigov.usuario_entidade add column if not exists tenant_id bigint null;
alter table sigov.usuario_exercicio add column if not exists tenant_id bigint null;
alter table sigov.politica_senha add column if not exists tenant_id bigint null;

alter table sigov.tenant_feature_flag add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_feature_flag add column if not exists feature_codigo varchar(120) null;
alter table sigov.tenant_feature_flag add column if not exists habilitada boolean not null default false;
alter table sigov.tenant_feature_flag add column if not exists parametros_json jsonb not null default '{}'::jsonb;

alter table sigov.tenant_parametro_valor add column if not exists usuario_id bigint null;
alter table sigov.tenant_parametro_valor add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_parametro_valor add column if not exists escopo varchar(40) not null default 'TENANT';

create unique index if not exists ux_bootstrap_usuario_login_tenant
    on sigov.usuario (tenant_id, lower(login)) where is_deleted = false;
create unique index if not exists ux_bootstrap_usuario_email_tenant
    on sigov.usuario (tenant_id, lower(email)) where is_deleted = false;
create unique index if not exists ux_bootstrap_perfil_codigo_tenant
    on sigov.perfil_acesso (tenant_id, codigo_externo) where codigo_externo is not null and is_deleted = false;
create unique index if not exists ux_bootstrap_grupo_nome_tenant
    on sigov.grupo_acesso (tenant_id, nome) where is_deleted = false;
