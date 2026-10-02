create schema if not exists sigov;

alter table if exists sigov.tenant add column if not exists email varchar(250) null;
alter table if exists sigov.tenant add column if not exists telefone varchar(40) null;
alter table if exists sigov.tenant add column if not exists plano varchar(80) null;
alter table if exists sigov.tenant add column if not exists cor_primaria varchar(20) null;
alter table if exists sigov.tenant add column if not exists logo_url text null;

alter table if exists sigov.usuario add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table if exists sigov.perfil_acesso add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table if exists sigov.usuario add column if not exists nome varchar(200) null;
alter table if exists sigov.usuario add column if not exists bloqueado boolean not null default false;
alter table if exists sigov.usuario add column if not exists deve_alterar_senha boolean not null default false;

create table if not exists sigov.auditoria_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    usuario_id bigint null references sigov.usuario(id),
    acao varchar(100) not null,
    entidade varchar(100) not null,
    entidade_id varchar(100) null,
    ip varchar(80) null,
    user_agent text null,
    antes jsonb null,
    depois jsonb null,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create index if not exists idx_auditoria_evento_created_at on sigov.auditoria_evento(created_at desc);
create index if not exists idx_auditoria_evento_tenant on sigov.auditoria_evento(tenant_id, created_at desc);
create index if not exists idx_auditoria_evento_acao on sigov.auditoria_evento(acao);

insert into sigov.tenant (nome, nome_fantasia, documento, slug, status, ambiente, ativo, email, telefone, plano, cor_primaria, logo_url, metadados)
values ('Plataforma SIGOV Global', 'SIGOV Global', '00000000000191', 'plataforma-global', 'ATIVO', 'PRODUCTION_BASELINE', true, null, null, 'global', '#1351b4', null, '{"seed":"pos-build-01","type":"structural"}'::jsonb)
on conflict (slug) do update set
    nome = excluded.nome,
    status = excluded.status,
    ativo = true,
    plano = excluded.plano,
    cor_primaria = excluded.cor_primaria,
    updated_at = now();

insert into sigov.perfil_acesso (tenant_id, nome, descricao, codigo_externo, ativo)
select t.id, 'Administrador Geral', 'Perfil global para administração SaaS inicial do SIGOV.', 'ADMINISTRADOR_GERAL', true
from sigov.tenant t
where t.slug = 'plataforma-global'
  and not exists (
      select 1 from sigov.perfil_acesso pa
      where pa.tenant_id = t.id and pa.codigo_externo = 'ADMINISTRADOR_GERAL' and pa.is_deleted = false
  );

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
    ('saas', 'tenants', 'visualizar', 'saas.tenants.visualizar', 'Visualizar tenants/clientes SaaS', true),
    ('saas', 'tenants', 'gerenciar', 'saas.tenants.gerenciar', 'Gerenciar tenants/clientes SaaS', true),
    ('saas', 'modulos', 'visualizar', 'saas.modulos.visualizar', 'Visualizar módulos SaaS', true),
    ('saas', 'modulos', 'gerenciar', 'saas.modulos.gerenciar', 'Gerenciar módulos contratados', true),
    ('operacao', 'health', 'visualizar', 'operacao.health.visualizar', 'Visualizar saúde do ambiente', true)
on conflict do nothing;

-- Pós-RC 20: a baseline estrutural não cria usuário, e-mail ou senha administrativa padrão.
-- Use scripts/create-initial-admin.ps1 ou scripts/create-initial-admin.sh explicitamente após a instalação.

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma-global' order by id limit 1) t
cross join sigov.permissao p
where pa.codigo_externo = 'ADMINISTRADOR_GERAL'
  and p.modulo in ('saas', 'operacao', 'core', 'seguranca', 'auditoria', 'lgpd')
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.tenant_modulo_contratado (tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ativo)
select t.id, m.codigo, case when m.codigo in ('integracoes','protocolo','ged') then 'EM_IMPLANTACAO' else 'DISPONIVEL' end, current_date, current_date, true
from sigov.tenant t
cross join (values ('tributario'),('rh'),('juridico'),('contratos'),('ged'),('protocolo'),('saude'),('educacao'),('agro'),('saneamento'),('social'),('integracoes')) as m(codigo)
where t.slug = 'plataforma-global'
on conflict (tenant_id, modulo_codigo) do nothing;

insert into sigov.auditoria_evento (tenant_id, usuario_id, acao, entidade, entidade_id, depois, correlation_id)
select t.id, u.id, 'SEED_ADMIN_POS_BUILD_01', 'sigov.usuario', u.id::varchar, jsonb_build_object('login', u.login, 'email', u.email), '00000000-0000-0000-0000-000000000001'::uuid
from sigov.tenant t
left join sigov.usuario u on false
where false;
