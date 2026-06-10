-- Evolução Pós-Build 03 - Planos SaaS, implantação guiada e base tributária parametrizável.
-- Migration idempotente: somente CREATE/ALTER IF NOT EXISTS, índices IF NOT EXISTS e seeds ON CONFLICT.

alter table sigov.saas_plano add column if not exists updated_at timestamptz null;
alter table sigov.saas_plano add column if not exists limite_tenants int null;
alter table sigov.saas_plano_modulo add column if not exists limite_especifico int null;
alter table sigov.saas_assinatura add column if not exists trial_ate date null;
alter table sigov.saas_assinatura add column if not exists observacao text null;

create table if not exists sigov.saas_assinatura_historico (
    id bigint generated always as identity primary key,
    assinatura_id bigint not null references sigov.saas_assinatura(id),
    tenant_id bigint not null,
    plano_anterior_id bigint null,
    plano_novo_id bigint null,
    acao varchar(80) not null,
    motivo text null,
    usuario_id bigint null,
    correlation_id uuid not null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.saas_implantacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    status varchar(40) not null default 'EM_ANDAMENTO',
    responsavel_nome varchar(200) null,
    responsavel_email varchar(200) null,
    data_inicio date not null default current_date,
    data_previsao date null,
    data_conclusao date null,
    percentual numeric(5,2) not null default 0,
    observacao text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id)
);

create table if not exists sigov.saas_implantacao_item (
    id bigint generated always as identity primary key,
    implantacao_id bigint not null references sigov.saas_implantacao(id),
    codigo varchar(100) not null,
    titulo varchar(200) not null,
    descricao text null,
    categoria varchar(80) null,
    obrigatorio boolean not null default true,
    concluido boolean not null default false,
    concluido_at timestamptz null,
    concluido_por bigint null,
    ordem int not null default 0,
    unique(implantacao_id, codigo)
);

create table if not exists sigov.saas_evento_comercial (
    id bigint generated always as identity primary key,
    tenant_id bigint null,
    tipo_evento varchar(80) not null,
    descricao text not null,
    origem varchar(80) null,
    usuario_id bigint null,
    payload jsonb null,
    correlation_id uuid not null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.tenant_parametro (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chave varchar(120) not null,
    valor text null,
    tipo varchar(40) not null default 'string',
    descricao text null,
    sensivel boolean not null default false,
    updated_at timestamptz null,
    unique(tenant_id, chave)
);

create table if not exists sigov.tributario_configuracao (
    tenant_id bigint primary key references sigov.tenant(id),
    inscricao_imobiliaria_mascara varchar(80) null,
    inscricao_mobiliaria_mascara varchar(80) null,
    usa_georreferenciamento boolean not null default false,
    usa_integracao_nfse boolean not null default false,
    usa_protesto boolean not null default false,
    updated_at timestamptz null
);

create table if not exists sigov.tributario_tipo_cadastro (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    codigo varchar(80) not null,
    nome varchar(200) not null,
    descricao text null,
    ativo boolean not null default true,
    unique(tenant_id, codigo)
);

create table if not exists sigov.tributario_campo_dinamico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    tipo_cadastro_codigo varchar(80) not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    tipo varchar(40) not null,
    obrigatorio boolean not null default false,
    ordem int not null default 0,
    opcoes_json jsonb null,
    ativo boolean not null default true,
    unique(tenant_id, tipo_cadastro_codigo, codigo)
);

create table if not exists sigov.tributario_contribuinte (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    nome varchar(200) not null,
    documento varchar(30) null,
    email varchar(200) null,
    telefone varchar(30) null,
    tipo_pessoa varchar(20) null,
    dados_json jsonb null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists sigov.tributario_imovel (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    inscricao varchar(120) not null,
    contribuinte_id bigint null references sigov.tributario_contribuinte(id),
    endereco_json jsonb null,
    area_terreno numeric(14,2) null,
    area_construida numeric(14,2) null,
    dados_json jsonb null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, inscricao)
);

create table if not exists sigov.tributario_economico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    inscricao varchar(120) not null,
    contribuinte_id bigint null references sigov.tributario_contribuinte(id),
    nome_fantasia varchar(200) null,
    atividade_principal varchar(200) null,
    dados_json jsonb null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, inscricao)
);

create index if not exists idx_saas_assinatura_tenant_status on sigov.saas_assinatura(tenant_id, status);
create unique index if not exists ux_saas_assinatura_ativa_tenant on sigov.saas_assinatura(tenant_id) where status = 'ATIVA';
create index if not exists idx_saas_assinatura_historico_tenant on sigov.saas_assinatura_historico(tenant_id, created_at desc);
create index if not exists idx_saas_implantacao_tenant_status on sigov.saas_implantacao(tenant_id, status);
create index if not exists idx_saas_implantacao_item_implantacao on sigov.saas_implantacao_item(implantacao_id, concluido);
create index if not exists idx_saas_evento_comercial_tenant_data on sigov.saas_evento_comercial(tenant_id, created_at desc);
create index if not exists idx_tenant_parametro_tenant on sigov.tenant_parametro(tenant_id, chave);
create index if not exists idx_tributario_contribuinte_tenant_nome on sigov.tributario_contribuinte(tenant_id, nome);
create index if not exists idx_tributario_imovel_tenant_inscricao on sigov.tributario_imovel(tenant_id, inscricao);
create index if not exists idx_tributario_economico_tenant_inscricao on sigov.tributario_economico(tenant_id, inscricao);
create index if not exists idx_tributario_campo_tenant_tipo on sigov.tributario_campo_dinamico(tenant_id, tipo_cadastro_codigo);

insert into sigov.saas_plano (codigo,nome,descricao,tipo_plano,preco_base,moeda,periodicidade,limite_usuarios,limite_tenants,limite_armazenamento_mb,permite_white_label,permite_dominio_customizado,publico,destaque,ativo,ordem)
values
('STARTER','Starter','Plano inicial para pequenos órgãos e implantação piloto.','MENSAL',0,'BRL','MENSAL',10,1,1024,false,false,true,false,true,10),
('GOV_BASIC','Governo Básico','Plano para secretarias e órgãos com gestão administrativa.','MENSAL',0,'BRL','MENSAL',50,1,10240,true,false,true,true,true,20),
('GOV_PLUS','Governo Plus','Plano completo para prefeituras e estruturas multiáreas.','MENSAL',0,'BRL','MENSAL',200,3,51200,true,false,true,true,true,30),
('ENTERPRISE','Enterprise','Plano corporativo com módulos ilimitados e domínio personalizado.','MENSAL',0,'BRL','MENSAL',null,null,null,true,true,false,false,true,40)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, limite_usuarios=excluded.limite_usuarios, limite_tenants=excluded.limite_tenants, permite_white_label=excluded.permite_white_label, permite_dominio_customizado=excluded.permite_dominio_customizado, publico=excluded.publico, destaque=excluded.destaque, ativo=true, updated_at=now();

with plano_modulos(codigo, modulo) as (
    values
    ('STARTER','dashboard'),('STARTER','seguranca'),('STARTER','auditoria'),('STARTER','protocolo'),('STARTER','ged'),
    ('GOV_BASIC','dashboard'),('GOV_BASIC','seguranca'),('GOV_BASIC','auditoria'),('GOV_BASIC','protocolo'),('GOV_BASIC','ged'),('GOV_BASIC','contratos'),('GOV_BASIC','rh'),
    ('GOV_PLUS','dashboard'),('GOV_PLUS','seguranca'),('GOV_PLUS','auditoria'),('GOV_PLUS','protocolo'),('GOV_PLUS','ged'),('GOV_PLUS','contratos'),('GOV_PLUS','rh'),('GOV_PLUS','tributario'),('GOV_PLUS','juridico'),('GOV_PLUS','saude'),('GOV_PLUS','educacao'),('GOV_PLUS','agro'),('GOV_PLUS','saneamento'),('GOV_PLUS','social'),('GOV_PLUS','integracoes'),
    ('ENTERPRISE','dashboard'),('ENTERPRISE','seguranca'),('ENTERPRISE','auditoria'),('ENTERPRISE','protocolo'),('ENTERPRISE','ged'),('ENTERPRISE','contratos'),('ENTERPRISE','rh'),('ENTERPRISE','tributario'),('ENTERPRISE','juridico'),('ENTERPRISE','saude'),('ENTERPRISE','educacao'),('ENTERPRISE','agro'),('ENTERPRISE','saneamento'),('ENTERPRISE','social'),('ENTERPRISE','integracoes')
)
insert into sigov.saas_plano_modulo (plano_id, modulo_codigo, incluso)
select p.id, pm.modulo, true from plano_modulos pm join sigov.saas_plano p on p.codigo=pm.codigo
on conflict (plano_id, modulo_codigo) do update set incluso=true;

insert into sigov.saas_plano_limite (plano_id,codigo,nome,valor,unidade,ilimitado)
select p.id, 'usuarios', 'Usuários ativos', p.limite_usuarios, 'usuarios', p.limite_usuarios is null from sigov.saas_plano p
on conflict (plano_id,codigo) do update set valor=excluded.valor, ilimitado=excluded.ilimitado;

insert into sigov.saas_assinatura (tenant_id, plano_id, status, data_inicio, usuarios_contratados, valor_contratado, periodicidade)
select t.id, p.id, 'ATIVA', current_date, 999999, 0, 'MENSAL'
from sigov.tenant t cross join sigov.saas_plano p
where p.codigo='ENTERPRISE' and (t.slug='plataforma' or t.slug='sigov' or t.nome ilike '%plataforma%')
on conflict do nothing;

insert into sigov.permissao (modulo,recurso,acao,chave,descricao,ativo)
values
('saas','planos','visualizar','saas.planos.visualizar','Visualizar planos SaaS',true),
('saas','planos','criar','saas.planos.criar','Criar planos SaaS',true),
('saas','planos','editar','saas.planos.editar','Editar planos SaaS',true),
('saas','planos','inativar','saas.planos.inativar','Inativar planos SaaS',true),
('saas','planos','modulos','saas.planos.modulos','Configurar módulos de planos SaaS',true),
('saas','planos','limites','saas.planos.limites','Configurar limites de planos SaaS',true),
('tributario','dashboard','visualizar','tributario.dashboard.visualizar','Visualizar dashboard tributário',true),
('tributario','configuracao','visualizar','tributario.configuracao.visualizar','Visualizar configuração tributária',true),
('tributario','configuracao','editar','tributario.configuracao.editar','Editar configuração tributária',true),
('tributario','tipos','visualizar','tributario.tipos.visualizar','Visualizar tipos tributários',true),
('tributario','tipos','editar','tributario.tipos.editar','Editar tipos tributários',true),
('tributario','campos','visualizar','tributario.campos.visualizar','Visualizar campos dinâmicos tributários',true),
('tributario','campos','editar','tributario.campos.editar','Editar campos dinâmicos tributários',true),
('tributario','contribuintes','visualizar','tributario.contribuintes.visualizar','Visualizar contribuintes',true),
('tributario','contribuintes','criar','tributario.contribuintes.criar','Criar contribuintes',true),
('tributario','contribuintes','editar','tributario.contribuintes.editar','Editar contribuintes',true),
('tributario','imoveis','visualizar','tributario.imoveis.visualizar','Visualizar imóveis',true),
('tributario','imoveis','criar','tributario.imoveis.criar','Criar imóveis',true),
('tributario','imoveis','editar','tributario.imoveis.editar','Editar imóveis',true),
('tributario','economicos','visualizar','tributario.economicos.visualizar','Visualizar econômicos',true),
('tributario','economicos','criar','tributario.economicos.criar','Criar econômicos',true),
('tributario','economicos','editar','tributario.economicos.editar','Editar econômicos',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.tenant_parametro (tenant_id,chave,valor,tipo,descricao,sensivel)
select t.id, p.chave, p.valor, p.tipo, p.descricao, p.sensivel
from sigov.tenant t
cross join (values
('orgao.nome',null,'string','Nome do órgão',false),
('orgao.documento',null,'string','Documento do órgão',false),
('orgao.email',null,'string','E-mail institucional',false),
('orgao.telefone',null,'string','Telefone institucional',false),
('orgao.endereco',null,'string','Endereço institucional',false),
('sistema.timezone','America/Sao_Paulo','string','Timezone padrão',false),
('sistema.locale','pt-BR','string','Locale padrão',false),
('sistema.moeda','BRL','string','Moeda padrão',false),
('lgpd.mascara_dados','true','boolean','Aplicar máscara LGPD',false),
('auditoria.retencao_dias','365','number','Retenção da auditoria',false)
) as p(chave,valor,tipo,descricao,sensivel)
on conflict (tenant_id,chave) do nothing;

insert into sigov.tributario_configuracao (tenant_id)
select id from sigov.tenant
on conflict (tenant_id) do nothing;

insert into sigov.tributario_tipo_cadastro (tenant_id,codigo,nome,descricao)
select t.id, v.codigo, v.nome, v.descricao
from sigov.tenant t
cross join (values
('CONTRIBUINTE','Contribuinte','Cadastro parametrizável de contribuintes'),
('IMOVEL','Imóvel','Cadastro imobiliário parametrizável'),
('ECONOMICO','Econômico','Cadastro econômico/mobiliário parametrizável')
) as v(codigo,nome,descricao)
on conflict (tenant_id,codigo) do nothing;
