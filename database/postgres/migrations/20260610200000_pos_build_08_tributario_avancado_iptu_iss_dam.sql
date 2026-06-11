-- SIGOV - Evolução Pós-Build 08: Tributário Avançado e Fiscal Integrado
-- Idempotente, schema sigov, multi-tenant, LGPD e auditoria fiscal.

create schema if not exists sigov;

create or replace function sigov.fn_tributario_avancado_set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create table if not exists sigov.contribuinte (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    inscricao varchar(80) not null,
    nome varchar(200) not null,
    documento varchar(30) null,
    tipo_pessoa varchar(20) not null default 'FISICA',
    email varchar(200) null,
    telefone varchar(40) null,
    endereco_json jsonb not null default '{}'::jsonb,
    consentimento_lgpd boolean not null default false,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, inscricao),
    unique (tenant_id, documento)
);

create table if not exists sigov.tributos_impostos (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    codigo varchar(80) not null,
    nome varchar(160) not null,
    tipo varchar(40) not null,
    aliquota numeric(9,4) null,
    fundamento_legal text null,
    vinculo_origem varchar(40) null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, codigo)
);

create table if not exists sigov.iptu (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    inscricao_imobiliaria varchar(120) not null,
    contribuinte_id bigint not null references sigov.contribuinte(id),
    exercicio int not null,
    valor_venal numeric(14,2) not null default 0,
    aliquota numeric(9,4) not null default 0,
    valor_lancado numeric(14,2) not null default 0,
    data_vencimento date not null,
    status varchar(40) not null default 'ABERTO',
    dados_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, inscricao_imobiliaria, exercicio)
);

create table if not exists sigov.iss (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    inscricao_municipal varchar(120) not null,
    contribuinte_id bigint not null references sigov.contribuinte(id),
    competencia date not null,
    base_calculo numeric(14,2) not null default 0,
    aliquota numeric(9,4) not null default 0,
    valor_lancado numeric(14,2) not null default 0,
    data_vencimento date not null,
    status varchar(40) not null default 'ABERTO',
    origem varchar(40) null,
    origem_id bigint null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, inscricao_municipal, competencia)
);

create table if not exists sigov.taxas_municipais (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    codigo varchar(80) not null,
    descricao varchar(220) not null,
    contribuinte_id bigint not null references sigov.contribuinte(id),
    inscricao varchar(120) not null,
    competencia date not null,
    valor numeric(14,2) not null default 0,
    data_vencimento date not null,
    status varchar(40) not null default 'ABERTO',
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, codigo, inscricao, competencia)
);

create table if not exists sigov.parcela (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    origem_tipo varchar(40) not null,
    origem_id bigint not null,
    contribuinte_id bigint not null references sigov.contribuinte(id),
    numero int not null,
    valor_original numeric(14,2) not null,
    valor_atualizado numeric(14,2) not null,
    data_vencimento date not null,
    status varchar(40) not null default 'ABERTA',
    conta_receber_id bigint null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, origem_tipo, origem_id, numero)
);

create table if not exists sigov.arrecadacao (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    parcela_id bigint not null references sigov.parcela(id),
    contribuinte_id bigint not null references sigov.contribuinte(id),
    valor_pago numeric(14,2) not null,
    data_pagamento timestamptz not null default now(),
    forma_pagamento varchar(40) not null default 'DAM',
    status varchar(40) not null default 'CONFIRMADA',
    codigo_baixa varchar(120) null,
    correlation_id uuid not null,
    usuario_id bigint null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, parcela_id, codigo_baixa)
);

create table if not exists sigov.documento_arrecadacao_municipal (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    numero varchar(120) not null,
    parcela_id bigint not null references sigov.parcela(id),
    contribuinte_id bigint not null references sigov.contribuinte(id),
    linha_digitavel varchar(180) not null,
    codigo_barras varchar(180) not null,
    valor numeric(14,2) not null,
    data_vencimento date not null,
    status varchar(40) not null default 'EMITIDO',
    emissao_simulada boolean not null default true,
    versao int not null default 1,
    historico_json jsonb not null default '[]'::jsonb,
    correlation_id uuid not null,
    usuario_id bigint null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, numero)
);

create table if not exists sigov.livro_eletronico_tributario (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    competencia date not null,
    tipo varchar(40) not null,
    versao int not null default 1,
    status varchar(40) not null default 'GERADO',
    total_lancado numeric(14,2) not null default 0,
    total_arrecadado numeric(14,2) not null default 0,
    registros_json jsonb not null default '[]'::jsonb,
    historico_json jsonb not null default '[]'::jsonb,
    gerado_por bigint null,
    correlation_id uuid not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, competencia, tipo, versao)
);

create table if not exists sigov.parcelamento_divida_ativa (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    numero varchar(120) not null,
    contribuinte_id bigint not null references sigov.contribuinte(id),
    inscricao_divida varchar(120) not null,
    valor_original numeric(14,2) not null,
    valor_atualizado numeric(14,2) not null,
    quantidade_parcelas int not null,
    status varchar(40) not null default 'ATIVO',
    termo_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, numero),
    unique (tenant_id, inscricao_divida)
);

create table if not exists sigov.integracao_nfse (
    id bigserial primary key,
    tenant_id bigint not null references sigov.tenant(id),
    contribuinte_id bigint not null references sigov.contribuinte(id),
    inscricao_municipal varchar(120) not null,
    rps_numero varchar(120) not null,
    nfse_numero varchar(120) null,
    competencia date not null,
    valor_servico numeric(14,2) not null,
    valor_iss numeric(14,2) not null default 0,
    status varchar(40) not null default 'SIMULADA',
    payload_json jsonb not null default '{}'::jsonb,
    resposta_json jsonb not null default '{}'::jsonb,
    correlation_id uuid not null,
    usuario_id bigint null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, inscricao_municipal, rps_numero)
);

create index if not exists idx_contribuinte_tenant_inscricao on sigov.contribuinte(tenant_id, inscricao);
create index if not exists idx_iptu_tenant_inscricao on sigov.iptu(tenant_id, inscricao_imobiliaria);
create index if not exists idx_iptu_tenant_vencimento on sigov.iptu(tenant_id, data_vencimento);
create index if not exists idx_iss_tenant_inscricao on sigov.iss(tenant_id, inscricao_municipal);
create index if not exists idx_iss_tenant_vencimento on sigov.iss(tenant_id, data_vencimento);
create index if not exists idx_taxas_tenant_inscricao on sigov.taxas_municipais(tenant_id, inscricao);
create index if not exists idx_taxas_tenant_vencimento on sigov.taxas_municipais(tenant_id, data_vencimento);
create index if not exists idx_parcela_tenant_vencimento on sigov.parcela(tenant_id, data_vencimento);
create index if not exists idx_parcela_tenant_status on sigov.parcela(tenant_id, status);
create index if not exists idx_arrecadacao_tenant_data on sigov.arrecadacao(tenant_id, data_pagamento desc);
create index if not exists idx_dam_tenant_vencimento on sigov.documento_arrecadacao_municipal(tenant_id, data_vencimento);
create index if not exists idx_livro_tenant_competencia on sigov.livro_eletronico_tributario(tenant_id, competencia desc);
create index if not exists idx_parcelamento_tenant_inscricao on sigov.parcelamento_divida_ativa(tenant_id, inscricao_divida);
create index if not exists idx_nfse_tenant_inscricao on sigov.integracao_nfse(tenant_id, inscricao_municipal);

DO $$
declare t text;
begin
    foreach t in array array['contribuinte','tributos_impostos','iptu','iss','taxas_municipais','parcela','arrecadacao','documento_arrecadacao_municipal','livro_eletronico_tributario','parcelamento_divida_ativa','integracao_nfse'] loop
        if not exists (
            select 1 from pg_trigger tr
            join pg_class c on c.oid = tr.tgrelid
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'sigov' and c.relname = t and tr.tgname = format('trg_%s_updated_at', t)
        ) then
            execute format('create trigger trg_%I_updated_at before update on sigov.%I for each row execute function sigov.fn_tributario_avancado_set_updated_at()', t, t);
        end if;
    end loop;
end $$;

insert into sigov.permissao (modulo,recurso,acao,chave,descricao,ativo) values
('tributario','dashboard','visualizar','tributario.dashboard.visualizar','Visualizar dashboard tributário avançado',true),
('tributario','iptu','visualizar','tributario.iptu.visualizar','Visualizar IPTU',true),
('tributario','iptu','editar','tributario.iptu.editar','Editar IPTU',true),
('tributario','iss','visualizar','tributario.iss.visualizar','Visualizar ISS',true),
('tributario','iss','editar','tributario.iss.editar','Editar ISS',true),
('tributario','taxas','visualizar','tributario.taxas.visualizar','Visualizar taxas municipais',true),
('tributario','taxas','editar','tributario.taxas.editar','Editar taxas municipais',true),
('tributario','divida_ativa','visualizar','tributario.divida_ativa.visualizar','Visualizar dívida ativa',true),
('tributario','divida_ativa','editar','tributario.divida_ativa.editar','Editar dívida ativa',true),
('tributario','parcelamento','visualizar','tributario.parcelamento.visualizar','Visualizar parcelamentos tributários',true),
('tributario','parcelamento','editar','tributario.parcelamento.editar','Editar parcelamentos tributários',true),
('tributario','arrecadacao','visualizar','tributario.arrecadacao.visualizar','Visualizar arrecadação tributária',true),
('tributario','arrecadacao','registrar','tributario.arrecadacao.registrar','Registrar arrecadação tributária',true),
('tributario','nfse','visualizar','tributario.nfse.visualizar','Visualizar NFS-e simulada',true),
('tributario','nfse','emitir','tributario.nfse.emitir','Emitir NFS-e simulada',true),
('tributario','livro_eletronico','visualizar','tributario.livro_eletronico.visualizar','Visualizar livro eletrônico tributário',true),
('tributario','livro_eletronico','gerar','tributario.livro_eletronico.gerar','Gerar livro eletrônico tributário',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.perfil_acesso (nome, descricao, codigo_externo, ativo) values
('Tributário Admin','Administra IPTU, ISS, taxas, DAM, dívida ativa, NFS-e simulada e livro eletrônico.','TRIBUTARIO_ADMIN',true),
('Fiscal Tributário','Opera lançamentos, arrecadação, parcelamentos e relatórios fiscais.','FISCAL_TRIBUTARIO',true),
('Consulta Tributária','Consulta dashboard, livro eletrônico e relatórios fiscais.','CONSULTA_TRIBUTARIA',true)
on conflict do nothing;

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.modulo='tributario' and p.ativo=true and p.is_deleted=false
where pa.ativo=true and pa.is_deleted=false
  and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('ADMIN_GERAL','ADMINISTRADOR_GERAL','ADMIN_TENANT','ADMINISTRADOR_TENANT','TRIBUTARIO_ADMIN')
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.chave in ('tributario.dashboard.visualizar','tributario.iptu.visualizar','tributario.iss.visualizar','tributario.taxas.visualizar','tributario.divida_ativa.visualizar','tributario.parcelamento.visualizar','tributario.arrecadacao.visualizar','tributario.arrecadacao.registrar','tributario.nfse.visualizar','tributario.livro_eletronico.visualizar','tributario.livro_eletronico.gerar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_')))='FISCAL_TRIBUTARIO'
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.chave in ('tributario.dashboard.visualizar','tributario.iptu.visualizar','tributario.iss.visualizar','tributario.taxas.visualizar','tributario.divida_ativa.visualizar','tributario.parcelamento.visualizar','tributario.arrecadacao.visualizar','tributario.nfse.visualizar','tributario.livro_eletronico.visualizar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_')))='CONSULTA_TRIBUTARIA'
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.tenant_modulo_pacote (codigo, nome, descricao, modulos_json) values
('GOV_TRIBUTARIO_PLUS','Gov Tributário Plus','Pacote fiscal municipal com Tributário Avançado, Financeiro Público e LGPD.', '["tributario","financeiro_publico","auditoria-lgpd"]'::jsonb)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, modulos_json=excluded.modulos_json;

insert into sigov.contribuinte (tenant_id, inscricao, nome, documento, tipo_pessoa, email, telefone, consentimento_lgpd)
select t.id, seed.inscricao, seed.nome, seed.documento, seed.tipo_pessoa, seed.email, seed.telefone, true
from sigov.tenant t
cross join (values
    ('MUN-000001','Contribuinte Exemplo IPTU','12345678901','FISICA','iptu.demo@sigov.local','(11) 3000-0001'),
    ('MUN-000002','Prestador Exemplo ISS','12345678000199','JURIDICA','iss.demo@sigov.local','(11) 3000-0002')
) as seed(inscricao,nome,documento,tipo_pessoa,email,telefone)
where t.slug in ('plataforma-global','prefeitura-demo','tenant-demo')
on conflict (tenant_id, inscricao) do update set nome=excluded.nome, email=excluded.email, telefone=excluded.telefone, updated_at=now();

insert into sigov.tributos_impostos (tenant_id, codigo, nome, tipo, aliquota, fundamento_legal, vinculo_origem)
select t.id, seed.codigo, seed.nome, seed.tipo, seed.aliquota, seed.fundamento_legal, seed.vinculo_origem
from sigov.tenant t
cross join (values
    ('IPTU','Imposto Predial e Territorial Urbano','IPTU',1.0000,'Código Tributário Municipal - IPTU','IMOBILIARIO'),
    ('ISS','Imposto Sobre Serviços','ISS',2.0000,'Código Tributário Municipal - ISS','SERVICO'),
    ('TAXA_COLETA','Taxa de Coleta de Resíduos','TAXA',0.0000,'Código Tributário Municipal - Taxas','SERVICO')
) as seed(codigo,nome,tipo,aliquota,fundamento_legal,vinculo_origem)
where t.slug in ('plataforma-global','prefeitura-demo','tenant-demo')
on conflict (tenant_id, codigo) do update set nome=excluded.nome, aliquota=excluded.aliquota, fundamento_legal=excluded.fundamento_legal, updated_at=now();
