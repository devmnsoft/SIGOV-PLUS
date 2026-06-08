-- Fundação SaaS parametrizável do sigov: schema único sigov, Dapper/PostgreSQL e isolamento por tenant.
alter table sigov.usuario add column if not exists tenant_id bigint null references sigov.tenant(id);
alter table sigov.usuario add column if not exists tipo_usuario varchar(80) null;
update sigov.usuario set tenant_id = e.tenant_id from sigov.entidade e where sigov.usuario.tenant_id is null and sigov.usuario.entidade_id = e.id;
create index if not exists idx_usuario_tenant on sigov.usuario (tenant_id) where is_deleted = false;

create table if not exists sigov.perfil_nivel (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(150) not null,
    descricao text null,
    nivel_hierarquico int not null,
    global boolean not null default false,
    tenant_admin boolean not null default false,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

insert into sigov.perfil_nivel (codigo, nome, descricao, nivel_hierarquico, global, tenant_admin) values
('ADMINISTRADOR_GERAL', 'Administrador Geral', 'Administra todo o sigov e todos os tenants com auditoria obrigatória.', 1000, true, false),
('ADMINISTRADOR_TENANT', 'Administrador do Tenant', 'Administra apenas o próprio tenant.', 900, false, true),
('ADMINISTRADOR_ENTIDADE', 'Administrador de Entidade', 'Administra uma entidade específica dentro do tenant.', 800, false, false),
('COORDENADOR', 'Coordenador', 'Coordena módulos, setores, equipes ou unidades conforme permissões.', 700, false, false),
('DIRETOR', 'Diretor', 'Gerencia unidade, escola, secretaria, departamento ou área conforme escopo.', 600, false, false),
('SERVIDOR', 'Servidor', 'Executa tarefas operacionais próprias ou atribuídas.', 500, false, false),
('OPERADOR', 'Operador', 'Executa permissões operacionais explícitas.', 400, false, false),
('CONSULTA', 'Consulta', 'Usuário somente leitura conforme escopo.', 300, false, false),
('AUDITOR', 'Auditor', 'Acessa auditoria e conformidade conforme permissão.', 200, false, false),
('SUPORTE', 'Suporte', 'Suporte técnico controlado e auditado.', 100, false, false)
on conflict (codigo) do update set nome = excluded.nome, descricao = excluded.descricao, nivel_hierarquico = excluded.nivel_hierarquico, global = excluded.global, tenant_admin = excluded.tenant_admin, ativo = true;

create table if not exists sigov.tenant_parametro_definicao (
    id bigint generated always as identity primary key,
    codigo varchar(150) not null unique,
    nome varchar(250) not null,
    descricao text null,
    modulo varchar(80) null,
    tipo_parametro varchar(40) not null,
    escopo varchar(40) not null,
    valor_padrao jsonb null,
    obrigatorio boolean not null default false,
    sensivel boolean not null default false,
    editavel_tenant boolean not null default true,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    constraint ck_tenant_parametro_tipo check (tipo_parametro in ('TEXTO','NUMERO','DECIMAL','BOOLEAN','DATA','JSON','SELECT','MULTISELECT')),
    constraint ck_tenant_parametro_escopo check (escopo in ('GLOBAL','TENANT','ENTIDADE','EXERCICIO','MODULO','USUARIO'))
);

create table if not exists sigov.tenant_parametro_valor (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    usuario_id bigint null references sigov.usuario(id),
    modulo_codigo varchar(80) null,
    escopo varchar(40) not null default 'TENANT',
    parametro_definicao_id bigint not null references sigov.tenant_parametro_definicao(id),
    valor jsonb not null default '{}'::jsonb,
    valor_mascarado text null,
    vigente_inicio date null,
    vigente_fim date null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    constraint ck_tenant_parametro_valor_escopo check (escopo in ('GLOBAL','TENANT','ENTIDADE','EXERCICIO','MODULO','USUARIO'))
);

alter table sigov.tenant_parametro_valor add column if not exists usuario_id bigint null references sigov.usuario(id);
alter table sigov.tenant_parametro_valor add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_parametro_valor add column if not exists escopo varchar(40) not null default 'TENANT';
create unique index if not exists ux_tenant_parametro_valor_resolucao on sigov.tenant_parametro_valor (tenant_id, parametro_definicao_id, escopo, coalesce(entidade_id, 0), coalesce(exercicio_id, 0), coalesce(usuario_id, 0), coalesce(modulo_codigo, '')) where ativo = true;

create table if not exists sigov.tenant_modulo_pacote (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(150) not null,
    descricao text null,
    modulos_json jsonb not null default '[]'::jsonb,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

insert into sigov.tenant_modulo_pacote (codigo, nome, descricao, modulos_json) values
('ESSENCIAL','Essencial','Fundação mínima da plataforma.','["core","seguranca","auditoria","lgpd","suporte"]'::jsonb),
('FINANCEIRO_TRIBUTARIO','Financeiro e Tributário','Gestão fiscal, arrecadação e relatórios.','["financeiro","tributario","relatorios"]'::jsonb),
('GESTAO_ADMINISTRATIVA','Gestão Administrativa','Backoffice administrativo municipal.','["processos","compras","contratos","almoxarifado","patrimonio","frotas","obras"]'::jsonb),
('SOCIAL_SAUDE_EDUCACAO','Social, Saúde e Educação','Políticas públicas integradas.','["educacao","saude","social"]'::jsonb),
('AGRO_RURAL','Agro Rural','Base futura rural integrada.','["agro","frotas","obras","tributario","relatorios"]'::jsonb),
('COMPLETO','Completo','Todos os módulos integrados do sigov.','["core","seguranca","auditoria","lgpd","processos","financeiro","tributario","compras","contratos","almoxarifado","patrimonio","frotas","obras","rh","educacao","saude","saneamento","social","relatorios","transparencia","integracoes","suporte","operacao","agro"]'::jsonb)
on conflict (codigo) do update set nome = excluded.nome, descricao = excluded.descricao, modulos_json = excluded.modulos_json, ativo = true;

create table if not exists sigov.tenant_modulo_contratado (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    modulo_codigo varchar(80) not null,
    pacote_codigo varchar(80) null,
    status varchar(40) not null,
    contratado_em date null,
    vigencia_inicio date null,
    vigencia_fim date null,
    parametros_json jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, modulo_codigo),
    constraint ck_tenant_modulo_contratado_status check (status in ('DISPONIVEL','CONTRATADO','HABILITADO','SUSPENSO','CANCELADO','EM_IMPLANTACAO','BETA'))
);

insert into sigov.tenant_modulo_contratado (tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ativo)
select tm.tenant_id, ms.codigo, case when tm.habilitado and tm.contratado then 'HABILITADO' when tm.contratado then 'CONTRATADO' else 'DISPONIVEL' end, current_date, current_date, tm.ativo
from sigov.tenant_modulo tm
join sigov.modulo_saas ms on ms.id = tm.modulo_saas_id
on conflict (tenant_id, modulo_codigo) do nothing;

create table if not exists sigov.tenant_feature_flag (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    modulo_codigo varchar(80) not null,
    feature_codigo varchar(120) not null,
    habilitada boolean not null default false,
    ambiente varchar(40) null,
    parametros_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, modulo_codigo, feature_codigo)
);

alter table sigov.tenant_feature_flag add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_feature_flag add column if not exists feature_codigo varchar(120) null;
alter table sigov.tenant_feature_flag add column if not exists ambiente varchar(40) null;
alter table sigov.tenant_feature_flag add column if not exists parametros_json jsonb not null default '{}'::jsonb;
update sigov.tenant_feature_flag tff set feature_codigo = ffd.codigo, modulo_codigo = coalesce(ffd.modulo, split_part(ffd.codigo, '.', 1)), parametros_json = coalesce(tff.valor, '{}'::jsonb) from sigov.feature_flag_def ffd where tff.feature_flag_def_id = ffd.id and tff.feature_codigo is null;
create unique index if not exists ux_tenant_feature_flag_codigo on sigov.tenant_feature_flag (tenant_id, modulo_codigo, feature_codigo) where feature_codigo is not null;

create table if not exists sigov.usuario_escopo_acesso (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    usuario_id bigint not null references sigov.usuario(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    modulo_codigo varchar(80) null,
    escopo varchar(40) not null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    created_by bigint null,
    constraint ck_usuario_escopo_acesso_escopo check (escopo in ('GLOBAL','TENANT','ENTIDADE','EXERCICIO','MODULO','UNIDADE','PROPRIO'))
);

create unique index if not exists ux_usuario_escopo_acesso_resolucao on sigov.usuario_escopo_acesso (tenant_id, usuario_id, coalesce(entidade_id, 0), coalesce(exercicio_id, 0), coalesce(modulo_codigo, ''), escopo) where ativo = true;

insert into sigov.usuario_escopo_acesso (tenant_id, usuario_id, entidade_id, exercicio_id, escopo)
select u.tenant_id, u.id, u.entidade_id, u.exercicio_id, case when u.entidade_id is not null then 'ENTIDADE' else 'TENANT' end
from sigov.usuario u
where u.tenant_id is not null
on conflict do nothing;

create table if not exists sigov.usuario_contexto_global_log (
    id bigint generated always as identity primary key,
    usuario_global_id bigint not null references sigov.usuario(id),
    tenant_destino_id bigint null references sigov.tenant(id),
    entidade_destino_id bigint null references sigov.entidade(id),
    motivo text not null,
    iniciado_at timestamptz not null default now(),
    finalizado_at timestamptz null,
    ip varchar(80) null,
    user_agent text null,
    correlation_id uuid null
);

create table if not exists sigov.modulo_dependencia (
    id bigint generated always as identity primary key,
    modulo_codigo varchar(80) not null,
    dependencia_modulo_codigo varchar(80) not null,
    obrigatoria boolean not null default true,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    unique (modulo_codigo, dependencia_modulo_codigo)
);

create table if not exists sigov.modulo_integracao_regra (
    id bigint generated always as identity primary key,
    modulo_origem_codigo varchar(80) not null,
    modulo_destino_codigo varchar(80) not null,
    regra_codigo varchar(120) not null,
    descricao text null,
    parametros_json jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    unique (modulo_origem_codigo, modulo_destino_codigo, regra_codigo)
);

insert into sigov.modulo_dependencia (modulo_codigo, dependencia_modulo_codigo) values
('seguranca','core'),('auditoria','core'),('lgpd','core'),('lgpd','auditoria'),('financeiro','core'),('tributario','core'),('agro','core'),('agro','tributario'),('integracoes','core'),('integracoes','auditoria')
on conflict (modulo_codigo, dependencia_modulo_codigo) do update set obrigatoria = true, ativo = true;

create index if not exists idx_tenant_parametro_valor_tenant on sigov.tenant_parametro_valor (tenant_id, parametro_definicao_id);
create index if not exists idx_tenant_modulo_contratado_tenant on sigov.tenant_modulo_contratado (tenant_id, modulo_codigo);
create index if not exists idx_tenant_feature_flag_tenant on sigov.tenant_feature_flag (tenant_id, modulo_codigo, feature_codigo);
create index if not exists idx_usuario_escopo_acesso_usuario on sigov.usuario_escopo_acesso (usuario_id, tenant_id);
create index if not exists idx_usuario_contexto_global_log_usuario on sigov.usuario_contexto_global_log (usuario_global_id, iniciado_at desc);
create index if not exists idx_usuario_contexto_global_log_tenant on sigov.usuario_contexto_global_log (tenant_destino_id, iniciado_at desc);
