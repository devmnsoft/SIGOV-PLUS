create table if not exists sigov.saas_plano (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(150) not null,
    descricao text null,
    publico boolean not null default true,
    destaque boolean not null default false,
    ordem int not null default 0,
    tipo_plano varchar(40) not null,
    preco_base numeric(18,2) null,
    moeda varchar(10) not null default 'BRL',
    periodicidade varchar(40) not null,
    limite_usuarios int null,
    limite_entidades int null,
    limite_armazenamento_mb int null,
    permite_white_label boolean not null default false,
    permite_dominio_customizado boolean not null default false,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    constraint ck_saas_plano_preco check (preco_base is null or preco_base >= 0),
    constraint ck_saas_plano_limites check ((limite_usuarios is null or limite_usuarios >= 0) and (limite_entidades is null or limite_entidades >= 0) and (limite_armazenamento_mb is null or limite_armazenamento_mb >= 0))
);

create table if not exists sigov.saas_plano_modulo (
    id bigint generated always as identity primary key,
    plano_id bigint not null references sigov.saas_plano(id),
    modulo_codigo varchar(80) not null,
    incluso boolean not null default true,
    obrigatorio boolean not null default false,
    limite_uso_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    unique(plano_id, modulo_codigo)
);

create table if not exists sigov.saas_plano_limite (
    id bigint generated always as identity primary key,
    plano_id bigint not null references sigov.saas_plano(id),
    codigo varchar(100) not null,
    nome varchar(150) not null,
    valor int null,
    unidade varchar(40) null,
    ilimitado boolean not null default false,
    created_at timestamptz not null default now(),
    unique(plano_id, codigo)
);

create table if not exists sigov.saas_addon (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(150) not null,
    descricao text null,
    tipo_addon varchar(40) not null,
    modulo_codigo varchar(80) null,
    preco numeric(18,2) null,
    periodicidade varchar(40) null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    constraint ck_saas_addon_preco check (preco is null or preco >= 0)
);

create table if not exists sigov.saas_plano_addon (
    id bigint generated always as identity primary key,
    plano_id bigint not null references sigov.saas_plano(id),
    addon_id bigint not null references sigov.saas_addon(id),
    incluso boolean not null default false,
    created_at timestamptz not null default now(),
    unique(plano_id, addon_id)
);

create table if not exists sigov.saas_assinatura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    plano_id bigint not null references sigov.saas_plano(id),
    status varchar(40) not null,
    data_inicio date not null default current_date,
    data_fim date null,
    usuarios_contratados int not null default 1,
    entidades_contratadas int null,
    valor_contratado numeric(18,2) null,
    moeda varchar(10) not null default 'BRL',
    periodicidade varchar(40) not null,
    renovacao_automatica boolean not null default false,
    parametros_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    constraint ck_saas_assinatura_usuarios check (usuarios_contratados > 0)
);

create table if not exists sigov.saas_assinatura_modulo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    assinatura_id bigint not null references sigov.saas_assinatura(id),
    modulo_codigo varchar(80) not null,
    status varchar(40) not null,
    habilitado boolean not null default true,
    vigencia_inicio date null,
    vigencia_fim date null,
    parametros_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    unique(tenant_id, assinatura_id, modulo_codigo)
);

create table if not exists sigov.saas_assinatura_addon (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    assinatura_id bigint not null references sigov.saas_assinatura(id),
    addon_codigo varchar(80) not null,
    quantidade int not null default 1,
    status varchar(40) not null default 'ATIVO',
    created_at timestamptz not null default now(),
    unique(tenant_id, assinatura_id, addon_codigo)
);

create table if not exists sigov.saas_solicitacao_cliente (
    id bigint generated always as identity primary key,
    protocolo varchar(80) not null unique,
    nome_organizacao varchar(250) not null,
    tipo_cliente varchar(80) not null,
    documento varchar(30) null,
    cidade varchar(150) null,
    uf varchar(2) null,
    nome_responsavel varchar(250) not null,
    email_responsavel varchar(250) not null,
    telefone_responsavel varchar(40) null,
    plano_codigo varchar(80) null,
    modulos_interesse jsonb not null default '[]'::jsonb,
    usuarios_estimados int null,
    entidades_estimadas int null,
    deseja_white_label boolean not null default false,
    deseja_dominio_customizado boolean not null default false,
    dominio_desejado varchar(250) null,
    status varchar(40) not null,
    tenant_id bigint null references sigov.tenant(id),
    observacao text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    correlation_id uuid null
);

create table if not exists sigov.saas_onboarding_cliente (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    solicitacao_id bigint null references sigov.saas_solicitacao_cliente(id),
    status varchar(40) not null default 'EM_ANDAMENTO',
    progresso_percentual int not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id)
);

create table if not exists sigov.saas_onboarding_tarefa (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    onboarding_id bigint not null references sigov.saas_onboarding_cliente(id),
    codigo varchar(100) not null,
    nome varchar(180) not null,
    ordem int not null default 0,
    concluida boolean not null default false,
    obrigatoria boolean not null default true,
    parametros_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, onboarding_id, codigo)
);

create table if not exists sigov.saas_tenant_branding (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    nome_exibicao varchar(150) not null,
    logo_url varchar(500) null,
    logo_storage_key varchar(500) null,
    cor_primaria varchar(20) null,
    cor_secundaria varchar(20) null,
    cor_acento varchar(20) null,
    tema varchar(40) not null default 'SIGOV',
    favicon_url varchar(500) null,
    css_customizado text null,
    white_label_ativo boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id)
);

create table if not exists sigov.saas_tenant_dominio (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    dominio varchar(250) not null,
    status varchar(40) not null,
    verificado boolean not null default false,
    token_verificacao varchar(150) null,
    ssl_status varchar(40) null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(dominio)
);

create table if not exists sigov.saas_tenant_parametro_inicial (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chave varchar(120) not null,
    valor_json jsonb not null default '{}'::jsonb,
    concluido boolean not null default false,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, chave)
);

create table if not exists sigov.saas_perfil_template (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(150) not null,
    nivel_base varchar(80) not null,
    descricao text null,
    permissoes_json jsonb not null default '[]'::jsonb,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists sigov.saas_perfil_template_permissao (
    id bigint generated always as identity primary key,
    template_id bigint not null references sigov.saas_perfil_template(id),
    permissao_codigo varchar(150) not null,
    modulo_codigo varchar(80) null,
    created_at timestamptz not null default now(),
    unique(template_id, permissao_codigo)
);

create table if not exists sigov.saas_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    tipo_evento varchar(150) not null,
    origem varchar(150) null,
    origem_id bigint null,
    payload jsonb not null default '{}'::jsonb,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create index if not exists idx_saas_plano_codigo on sigov.saas_plano(codigo);
create index if not exists idx_saas_assinatura_tenant on sigov.saas_assinatura(tenant_id);
create index if not exists idx_saas_assinatura_status on sigov.saas_assinatura(status);
create index if not exists idx_saas_solicitacao_status on sigov.saas_solicitacao_cliente(status);
create index if not exists idx_saas_solicitacao_email on sigov.saas_solicitacao_cliente(email_responsavel);
create index if not exists idx_saas_branding_tenant on sigov.saas_tenant_branding(tenant_id);
create index if not exists idx_saas_dominio_tenant on sigov.saas_tenant_dominio(tenant_id);
create index if not exists idx_saas_dominio_dominio on sigov.saas_tenant_dominio(dominio);
create index if not exists idx_saas_evento_tipo on sigov.saas_evento(tipo_evento);

insert into sigov.saas_plano (codigo,nome,descricao,publico,destaque,ordem,tipo_plano,preco_base,periodicidade,limite_usuarios,limite_entidades,limite_armazenamento_mb,permite_white_label,permite_dominio_customizado)
values
('ESSENCIAL','Essencial','Core, Segurança, Auditoria/LGPD e suporte básico.',true,false,10,'PUBLICO',null,'MENSAL',20,2,10240,false,false),
('FINANCEIRO_TRIBUTARIO','Financeiro e Tributário','Pacote para financeiro, tributário e relatórios.',true,false,20,'PUBLICO',null,'MENSAL',50,5,20480,false,false),
('GESTAO_ADMINISTRATIVA','Gestão Administrativa','Processos, compras, contratos, almoxarifado, patrimônio, frotas e obras.',true,false,30,'PUBLICO',null,'MENSAL',100,10,51200,false,false),
('SOCIAL_SAUDE_EDUCACAO','Social, Saúde e Educação','Educação, saúde e assistência social integradas.',true,false,40,'PUBLICO',null,'MENSAL',150,20,102400,false,false),
('AGRO_RURAL','Agro Rural','Agro, frotas, obras e relatórios com tributário opcional.',true,false,50,'PUBLICO',null,'MENSAL',60,10,51200,false,false),
('COMPLETO','Completo','Todos os módulos do sigov com white label e domínio customizado.',true,true,60,'PUBLICO',null,'MENSAL',300,50,204800,true,true),
('ENTERPRISE','Enterprise','Plano personalizado para operação municipal avançada.',true,false,70,'ENTERPRISE',null,'PERSONALIZADA',null,null,null,true,true)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, publico=excluded.publico, destaque=excluded.destaque, ordem=excluded.ordem, permite_white_label=excluded.permite_white_label, permite_dominio_customizado=excluded.permite_dominio_customizado;

insert into sigov.saas_addon (codigo,nome,descricao,tipo_addon,modulo_codigo,preco,periodicidade)
values
('USUARIOS_EXTRAS','Usuários extras','Pacote adicional de usuários.','USUARIOS_EXTRAS',null,null,'MENSAL'),
('ARMAZENAMENTO_EXTRA','Armazenamento extra','Armazenamento adicional.','ARMAZENAMENTO_EXTRA',null,null,'MENSAL'),
('WHITE_LABEL','White label','Identidade visual própria do tenant.','WHITE_LABEL',null,null,'MENSAL'),
('DOMINIO_CUSTOMIZADO','Domínio customizado','Domínio estrutural customizado.','DOMINIO_CUSTOMIZADO',null,null,'MENSAL'),
('SUPORTE_PREMIUM','Suporte premium','Suporte técnico ampliado.','SUPORTE_PREMIUM',null,null,'MENSAL'),
('TRIBUTARIO_EXTRA','Tributário opcional','Módulo Tributário adicional.','MODULO_EXTRA','tributario',null,'MENSAL')
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, tipo_addon=excluded.tipo_addon, modulo_codigo=excluded.modulo_codigo;

insert into sigov.saas_plano_modulo (plano_id, modulo_codigo, incluso, obrigatorio)
select p.id, m.modulo_codigo, true, m.obrigatorio
from sigov.saas_plano p
join (values
('ESSENCIAL','core',true),('ESSENCIAL','seguranca',true),('ESSENCIAL','auditoria',true),('ESSENCIAL','lgpd',true),('ESSENCIAL','suporte',true),
('FINANCEIRO_TRIBUTARIO','financeiro',true),('FINANCEIRO_TRIBUTARIO','tributario',true),('FINANCEIRO_TRIBUTARIO','relatorios',true),
('GESTAO_ADMINISTRATIVA','processos',true),('GESTAO_ADMINISTRATIVA','compras',true),('GESTAO_ADMINISTRATIVA','contratos',true),('GESTAO_ADMINISTRATIVA','almoxarifado',true),('GESTAO_ADMINISTRATIVA','patrimonio',true),('GESTAO_ADMINISTRATIVA','frotas',true),('GESTAO_ADMINISTRATIVA','obras',true),
('SOCIAL_SAUDE_EDUCACAO','educacao',true),('SOCIAL_SAUDE_EDUCACAO','saude',true),('SOCIAL_SAUDE_EDUCACAO','social',true),
('AGRO_RURAL','agro',true),('AGRO_RURAL','frotas',true),('AGRO_RURAL','obras',true),('AGRO_RURAL','relatorios',true),('AGRO_RURAL','tributario',false),
('COMPLETO','core',true),('COMPLETO','seguranca',true),('COMPLETO','auditoria',true),('COMPLETO','lgpd',true),('COMPLETO','financeiro',true),('COMPLETO','tributario',true),('COMPLETO','processos',true),('COMPLETO','compras',true),('COMPLETO','contratos',true),('COMPLETO','almoxarifado',true),('COMPLETO','patrimonio',true),('COMPLETO','frotas',true),('COMPLETO','obras',true),('COMPLETO','educacao',true),('COMPLETO','saude',true),('COMPLETO','social',true),('COMPLETO','agro',true),('COMPLETO','relatorios',true)
) as m(plano_codigo, modulo_codigo, obrigatorio) on m.plano_codigo = p.codigo
on conflict (plano_id, modulo_codigo) do update set incluso=excluded.incluso, obrigatorio=excluded.obrigatorio;

insert into sigov.saas_plano_limite (plano_id,codigo,nome,valor,unidade,ilimitado)
select p.id, l.codigo, l.nome, l.valor, l.unidade, l.ilimitado
from sigov.saas_plano p
join (values
('usuarios','Usuários',null,'usuarios',false),('entidades','Entidades',null,'entidades',false),('armazenamento_mb','Armazenamento',null,'MB',false),('api_requests_mes','Requisições API/mês',50000,'requests',false),('relatorios_agendados','Relatórios agendados',10,'relatórios',false),('integracoes','Integrações',3,'integrações',false),('white_label','White label',null,null,false),('dominio_customizado','Domínio customizado',null,null,false)
) as l(codigo,nome,valor,unidade,ilimitado) on true
on conflict (plano_id, codigo) do nothing;

insert into sigov.saas_perfil_template (codigo,nome,nivel_base,descricao,permissoes_json)
values
('ADMINISTRADOR_TENANT','Administrador do Tenant','ADMINISTRADOR_TENANT','Administra apenas o tenant.', '["tenant.assinatura.visualizar","tenant.branding.visualizar","tenant.branding.editar","tenant.dominio.visualizar","tenant.dominio.gerenciar"]'::jsonb),
('COORDENADOR','Coordenador','COORDENADOR','Coordena módulos, setores ou unidades permitidas.', '[]'::jsonb),
('DIRETOR','Diretor','DIRETOR','Gerencia unidade/departamento/secretaria/escola/setor permitido.', '[]'::jsonb),
('SERVIDOR','Servidor','SERVIDOR','Executa tarefas operacionais.', '[]'::jsonb),
('OPERADOR','Operador','OPERADOR','Executa rotinas limitadas.', '[]'::jsonb),
('CONSULTA','Consulta','CONSULTA','Acesso somente leitura.', '[]'::jsonb),
('AUDITOR','Auditor','AUDITOR','Acessa auditoria e conformidade conforme permissão.', '["saas.assinaturas.visualizar"]'::jsonb)
on conflict (codigo) do update set nome=excluded.nome, nivel_base=excluded.nivel_base, descricao=excluded.descricao, permissoes_json=excluded.permissoes_json, ativo=true;
