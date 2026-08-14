-- RC50.36: núcleo financeiro/SIAFIC preparatório. Não representa homologação oficial.
create schema if not exists sigov;

create table if not exists sigov.financeiro_exercicio (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, codigo varchar(20) not null,
 nome varchar(160) not null, ano integer not null, status varchar(24) not null default 'RASCUNHO', data_abertura date,
 data_encerramento date, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb,
 correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz,
 created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id, entidade_id, ano)
);

do $$
declare t text;
begin
 foreach t in array array['financeiro_unidade_orcamentaria','financeiro_programa','financeiro_acao','financeiro_fonte_recurso','financeiro_natureza_despesa','financeiro_elemento_despesa','financeiro_centro_custo'] loop
  execute format('create table if not exists sigov.%I (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint references sigov.financeiro_exercicio(id), codigo varchar(60) not null, nome varchar(180) not null, descricao text, status varchar(24) not null default ''ABERTO'', dados jsonb not null default ''{}''::jsonb, auditoria jsonb not null default ''{}''::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id, entidade_id, exercicio_id, codigo))', t);
 end loop;
end $$;

create table if not exists sigov.financeiro_dotacao (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null references sigov.financeiro_exercicio(id), codigo varchar(60) not null,
 unidade_orcamentaria_id bigint references sigov.financeiro_unidade_orcamentaria(id), programa_id bigint references sigov.financeiro_programa(id), acao_id bigint references sigov.financeiro_acao(id),
 fonte_recurso_id bigint not null references sigov.financeiro_fonte_recurso(id), natureza_despesa_id bigint not null references sigov.financeiro_natureza_despesa(id), centro_custo_id bigint references sigov.financeiro_centro_custo(id),
 valor_previsto numeric(18,2) not null check(valor_previsto>=0), valor_atualizado numeric(18,2) not null check(valor_atualizado>=0), valor_empenhado numeric(18,2) not null default 0,
 valor_liquidado numeric(18,2) not null default 0, valor_pago numeric(18,2) not null default 0, saldo numeric(18,2) generated always as (valor_atualizado-valor_empenhado) stored,
 status varchar(24) not null default 'ABERTO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint,
 unique(tenant_id, entidade_id, exercicio_id, codigo), check(valor_liquidado<=valor_empenhado), check(valor_pago<=valor_liquidado)
);

create table if not exists sigov.financeiro_empenho (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null references sigov.financeiro_exercicio(id), dotacao_id bigint not null references sigov.financeiro_dotacao(id),
 codigo varchar(60) not null, credor_id bigint, credor_nome varchar(180) not null, descricao text not null, competencia date, valor numeric(18,2) not null check(valor>0), valor_liquidado numeric(18,2) not null default 0,
 valor_pago numeric(18,2) not null default 0, status varchar(24) not null default 'RASCUNHO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100),
 ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint,
 unique(tenant_id, entidade_id, exercicio_id, codigo), check(valor_liquidado<=valor), check(valor_pago<=valor_liquidado)
);

create table if not exists sigov.financeiro_empenho_item (id bigserial primary key, tenant_id bigint not null, empenho_id bigint not null references sigov.financeiro_empenho(id), descricao text not null, quantidade numeric(18,4) not null check(quantidade>0), valor_unitario numeric(18,4) not null check(valor_unitario>0), dados jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now());
create table if not exists sigov.financeiro_liquidacao (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null references sigov.financeiro_exercicio(id), empenho_id bigint not null references sigov.financeiro_empenho(id), codigo varchar(60) not null, descricao text not null, competencia date, valor numeric(18,2) not null check(valor>0), status varchar(24) not null default 'RASCUNHO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id,entidade_id,exercicio_id,codigo));
create table if not exists sigov.financeiro_pagamento (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null references sigov.financeiro_exercicio(id), liquidacao_id bigint not null references sigov.financeiro_liquidacao(id), conta_bancaria_id bigint, codigo varchar(60) not null, descricao text not null, competencia date, data_pagamento date, valor numeric(18,2) not null check(valor>0), status varchar(24) not null default 'RASCUNHO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id,entidade_id,exercicio_id,codigo));

do $$ declare t text; begin foreach t in array array['financeiro_movimento_orcamentario','financeiro_receita','financeiro_conta_bancaria','financeiro_conciliacao','financeiro_integracao_interna','financeiro_evento'] loop execute format('create table if not exists sigov.%I (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint, codigo varchar(80), descricao text, competencia date, valor numeric(18,2) not null default 0, status varchar(24) not null default ''PENDENTE'', dados jsonb not null default ''{}''::jsonb, auditoria jsonb not null default ''{}''::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint)',t); end loop; end $$;
create unique index if not exists ux_fin_integracao_origem on sigov.financeiro_integracao_interna(tenant_id,(dados->>'origem'),(dados->>'origem_id')) where is_deleted=false;
create index if not exists ix_fin_dotacao_tenant_status on sigov.financeiro_dotacao(tenant_id,status) where is_deleted=false;
create index if not exists ix_fin_empenho_exercicio on sigov.financeiro_empenho(tenant_id,exercicio_id,created_at) where is_deleted=false;
create index if not exists ix_fin_pagamento_competencia on sigov.financeiro_pagamento(tenant_id,competencia,status) where is_deleted=false;
