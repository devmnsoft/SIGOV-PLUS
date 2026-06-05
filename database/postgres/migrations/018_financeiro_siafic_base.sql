create table if not exists sigov.financeiro_sequencial (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint not null references sigov.exercicio(id),
    ano int not null,
    escopo varchar(60) not null,
    ultimo_numero int not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, entidade_id, exercicio_id, ano, escopo)
);

create table if not exists sigov.plano_contas (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(80) not null, nome varchar(250) not null, tipo_conta varchar(40) not null, nivel int not null,
    conta_pai_id bigint null references sigov.plano_contas(id), natureza_saldo varchar(20) null, aceita_lancamento boolean not null default true,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.fonte_recurso (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(50) not null, nome varchar(250) not null, descricao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.orgao_unidade_orcamentaria (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(50) not null, nome varchar(250) not null, sigla varchar(50) null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.programa (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(50) not null, nome varchar(250) not null, objetivo text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.acao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    programa_id bigint not null references sigov.programa(id), codigo varchar(50) not null, nome varchar(250) not null, tipo_acao varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.natureza_receita (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(80) not null, nome varchar(250) not null, categoria varchar(80) null, origem varchar(80) null, especie varchar(80) null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.natureza_despesa (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    codigo varchar(80) not null, nome varchar(250) not null, categoria varchar(80) null, grupo varchar(80) null, modalidade varchar(80) null, elemento varchar(80) null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, codigo)
);
create table if not exists sigov.orcamento_despesa (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    orgao_unidade_orcamentaria_id bigint null references sigov.orgao_unidade_orcamentaria(id), programa_id bigint not null references sigov.programa(id), acao_id bigint not null references sigov.acao(id), natureza_despesa_id bigint not null references sigov.natureza_despesa(id), fonte_recurso_id bigint not null references sigov.fonte_recurso(id),
    dotacao_inicial numeric(18,2) not null default 0 check (dotacao_inicial >= 0), suplementacoes numeric(18,2) not null default 0, reducoes numeric(18,2) not null default 0, reservado numeric(18,2) not null default 0, empenhado numeric(18,2) not null default 0, liquidado numeric(18,2) not null default 0, pago numeric(18,2) not null default 0,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.orcamento_receita (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id),
    natureza_receita_id bigint not null references sigov.natureza_receita(id), fonte_recurso_id bigint not null references sigov.fonte_recurso(id), previsao_inicial numeric(18,2) not null default 0, previsao_atualizada numeric(18,2) not null default 0, lancado numeric(18,2) not null default 0, arrecadado numeric(18,2) not null default 0,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.orcamento_movimentacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), orcamento_despesa_id bigint not null references sigov.orcamento_despesa(id), tipo_movimentacao varchar(40) not null, valor numeric(18,2) not null check (valor > 0), historico text not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.empenho (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), orcamento_despesa_id bigint not null references sigov.orcamento_despesa(id), numero varchar(50) not null, ano int not null, data_empenho date not null, fornecedor_pessoa_id bigint not null references sigov.pessoa(id), historico text not null, tipo_empenho varchar(40) not null, valor_total numeric(18,2) not null, valor_anulado numeric(18,2) not null default 0, valor_liquidado numeric(18,2) not null default 0, valor_pago numeric(18,2) not null default 0, status varchar(40) not null, metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, numero)
);
create table if not exists sigov.empenho_item (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), empenho_id bigint not null references sigov.empenho(id), descricao varchar(500) not null, quantidade numeric(18,4) not null default 1, valor_unitario numeric(18,2) not null, valor_total numeric(18,2) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.liquidacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), empenho_id bigint not null references sigov.empenho(id), numero varchar(50) not null, data_liquidacao date not null, documento_fiscal varchar(100) null, historico text not null, valor numeric(18,2) not null, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, numero)
);
create table if not exists sigov.pagamento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), liquidacao_id bigint not null references sigov.liquidacao(id), numero varchar(50) not null, data_pagamento date not null, forma_pagamento varchar(40) not null, conta_bancaria varchar(100) null, historico text not null, valor numeric(18,2) not null, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, numero)
);
create table if not exists sigov.receita_lancamento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), orcamento_receita_id bigint not null references sigov.orcamento_receita(id), numero varchar(50) not null, data_lancamento date not null, contribuinte_pessoa_id bigint null references sigov.pessoa(id), historico text not null, valor numeric(18,2) not null, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, numero)
);
create table if not exists sigov.receita_arrecadacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), receita_lancamento_id bigint not null references sigov.receita_lancamento(id), numero varchar(50) not null, data_arrecadacao date not null, forma_arrecadacao varchar(40) not null, valor numeric(18,2) not null, historico text not null, status varchar(40) not null default 'ARRECADADA',
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, exercicio_id, numero)
);
create table if not exists sigov.prestacao_contas (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), competencia varchar(20) not null, status varchar(40) not null default 'ABERTA', metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);
create table if not exists sigov.integracao_financeira_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint not null references sigov.exercicio(id), tipo_evento varchar(150) not null, payload jsonb not null, status varchar(40) not null default 'PENDENTE',
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create index if not exists idx_plano_contas_tenant_codigo on sigov.plano_contas (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_fonte_recurso_tenant_codigo on sigov.fonte_recurso (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_programa_tenant_codigo on sigov.programa (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_acao_tenant_codigo on sigov.acao (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_natureza_despesa_tenant_codigo on sigov.natureza_despesa (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_natureza_receita_tenant_codigo on sigov.natureza_receita (tenant_id, entidade_id, exercicio_id, codigo);
create index if not exists idx_orcamento_despesa_tenant_exercicio on sigov.orcamento_despesa (tenant_id, entidade_id, exercicio_id);
create index if not exists idx_orcamento_receita_tenant_exercicio on sigov.orcamento_receita (tenant_id, entidade_id, exercicio_id);
create index if not exists idx_empenho_tenant_numero on sigov.empenho (tenant_id, entidade_id, exercicio_id, numero);
create index if not exists idx_empenho_tenant_fornecedor on sigov.empenho (tenant_id, fornecedor_pessoa_id);
create index if not exists idx_empenho_tenant_status on sigov.empenho (tenant_id, status);
create index if not exists idx_empenho_tenant_data on sigov.empenho (tenant_id, data_empenho);
create index if not exists idx_liquidacao_tenant_numero on sigov.liquidacao (tenant_id, entidade_id, exercicio_id, numero);
create index if not exists idx_liquidacao_tenant_empenho on sigov.liquidacao (tenant_id, empenho_id);
create index if not exists idx_pagamento_tenant_numero on sigov.pagamento (tenant_id, entidade_id, exercicio_id, numero);
create index if not exists idx_pagamento_tenant_liquidacao on sigov.pagamento (tenant_id, liquidacao_id);
create index if not exists idx_receita_lancamento_tenant_numero on sigov.receita_lancamento (tenant_id, entidade_id, exercicio_id, numero);
create index if not exists idx_receita_arrecadacao_tenant_numero on sigov.receita_arrecadacao (tenant_id, entidade_id, exercicio_id, numero);

create or replace view sigov.vw_financeiro_resumo_orcamento as
select tenant_id, entidade_id, exercicio_id, sum(dotacao_inicial + suplementacoes - reducoes) as orcamento_autorizado, sum(empenhado) as empenhado, sum(liquidado) as liquidado, sum(pago) as pago, sum(dotacao_inicial + suplementacoes - reducoes - reservado - empenhado) as saldo_disponivel
from sigov.orcamento_despesa where is_deleted = false group by tenant_id, entidade_id, exercicio_id;
create or replace view sigov.vw_financeiro_execucao_despesa as
select od.*, (od.dotacao_inicial + od.suplementacoes - od.reducoes - od.reservado - od.empenhado) as saldo_disponivel from sigov.orcamento_despesa od where od.is_deleted = false;
create or replace view sigov.vw_financeiro_execucao_receita as
select ore.*, (ore.previsao_atualizada - ore.arrecadado) as saldo_a_arrecadar from sigov.orcamento_receita ore where ore.is_deleted = false;
create or replace view sigov.vw_financeiro_dashboard as
select d.tenant_id, d.entidade_id, d.exercicio_id, d.orcamento_autorizado, d.empenhado, d.liquidado, d.pago, d.saldo_disponivel, coalesce(r.receita_prevista,0) as receita_prevista, coalesce(r.receita_lancada,0) as receita_lancada, coalesce(r.receita_arrecadada,0) as receita_arrecadada
from sigov.vw_financeiro_resumo_orcamento d
left join (select tenant_id, entidade_id, exercicio_id, sum(previsao_atualizada) as receita_prevista, sum(lancado) as receita_lancada, sum(arrecadado) as receita_arrecadada from sigov.orcamento_receita where is_deleted = false group by tenant_id, entidade_id, exercicio_id) r on r.tenant_id=d.tenant_id and r.entidade_id=d.entidade_id and r.exercicio_id=d.exercicio_id;

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo)
values ('financeiro', 'Financeiro/SIAFIC', 'Base financeira e orçamentária SIAFIC do sigov.', 'Operacional', 20, '/Financeiro', 'cash-coin', true)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, rota_base=excluded.rota_base, ativo=true, is_deleted=false;

insert into sigov.permissao (modulo, chave, recurso, acao, descricao, ativo)
select 'financeiro', p.chave, split_part(p.chave,'.',1)||'.'||split_part(p.chave,'.',2), split_part(p.chave,'.',3), p.descricao, true
from (values
 ('financeiro.plano_contas.visualizar','Visualizar plano de contas'),('financeiro.plano_contas.criar','Criar plano de contas'),('financeiro.plano_contas.editar','Editar plano de contas'),('financeiro.plano_contas.excluir','Excluir plano de contas'),
 ('financeiro.orcamento.visualizar','Visualizar orçamento'),('financeiro.orcamento.criar','Criar orçamento'),('financeiro.orcamento.editar','Editar orçamento'),('financeiro.orcamento.movimentar','Movimentar orçamento'),
 ('financeiro.empenho.visualizar','Visualizar empenhos'),('financeiro.empenho.criar','Criar empenhos'),('financeiro.empenho.editar','Editar empenhos'),('financeiro.empenho.anular','Anular empenhos'),
 ('financeiro.liquidacao.visualizar','Visualizar liquidações'),('financeiro.liquidacao.criar','Criar liquidações'),('financeiro.liquidacao.anular','Anular liquidações'),
 ('financeiro.pagamento.visualizar','Visualizar pagamentos'),('financeiro.pagamento.criar','Criar pagamentos'),('financeiro.pagamento.cancelar','Cancelar pagamentos'),
 ('financeiro.receita.visualizar','Visualizar receitas'),('financeiro.receita.criar','Criar receitas'),('financeiro.receita.arrecadar','Arrecadar receitas'),
 ('financeiro.dashboard.visualizar','Visualizar dashboard financeiro'),('financeiro.exportar','Exportar dados financeiros')
) as p(chave, descricao)
on conflict (modulo, chave) do update set recurso=excluded.recurso, acao=excluded.acao, descricao=excluded.descricao, ativo=true, is_deleted=false;

insert into sigov.fonte_recurso (tenant_id, entidade_id, exercicio_id, codigo, nome, descricao)
select t.id, e.id, ex.id, '1500', 'Recursos não vinculados de impostos', 'Seed financeiro básico'
from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id and e.is_deleted=false join sigov.exercicio ex on ex.entidade_id=e.id and ex.is_deleted=false
where t.is_deleted=false on conflict do nothing;
