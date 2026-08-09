-- RC48: autenticação administrativa. Idempotente e sem credenciais ou dados demonstrativos.
alter table sigov.usuario add column if not exists deve_alterar_senha boolean not null default false;
alter table sigov.usuario add column if not exists bloqueado boolean not null default false;

create table if not exists sigov.senha_redefinicao_token (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    usuario_id bigint not null references sigov.usuario(id),
    token_hash varchar(64) not null,
    expira_at timestamptz not null,
    usado_at timestamptz null,
    created_at timestamptz not null default now(),
    correlation_id uuid not null
);
create unique index if not exists ux_senha_redefinicao_token_hash on sigov.senha_redefinicao_token(token_hash);
create index if not exists ix_senha_redefinicao_token_usuario on sigov.senha_redefinicao_token(tenant_id, usuario_id, created_at desc);
revoke all on sigov.senha_redefinicao_token from public;
