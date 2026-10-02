-- SIGOV Pós-Build 05: Comércio varejista/atacadista avançado, PDV, caixa, estoque integrado e financeiro inicial.
-- Migration idempotente: CREATE TABLE/INDEX IF NOT EXISTS e seeds com ON CONFLICT, sem DROP destrutivo.
create schema if not exists sigov;

create table if not exists sigov.comercio_cliente (
    id bigserial primary key,
    tenant_id bigint not null,
    nome varchar(200) not null,
    tipo_pessoa varchar(20) null,
    documento varchar(30) null,
    email varchar(200) null,
    telefone varchar(30) null,
    endereco_json jsonb null,
    limite_credito numeric(14,2) null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists sigov.comercio_vendedor (
    id bigserial primary key,
    tenant_id bigint not null,
    nome varchar(200) not null,
    usuario_id bigint null,
    percentual_comissao numeric(8,4) not null default 0,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists sigov.comercio_representante (
    id bigserial primary key,
    tenant_id bigint not null,
    nome varchar(200) not null,
    documento varchar(30) null,
    email varchar(200) null,
    telefone varchar(30) null,
    percentual_comissao numeric(8,4) not null default 0,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null
);

create table if not exists sigov.comercio_produto (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    descricao text null,
    unidade varchar(20) not null default 'UN',
    codigo_barras varchar(80) null,
    preco_venda numeric(14,2) not null default 0,
    preco_custo numeric(14,2) null,
    controla_estoque boolean not null default true,
    gera_os boolean not null default false,
    estoque_minimo numeric(14,4) not null default 0,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.comercio_tabela_preco (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    tipo varchar(40) not null default 'ATACADO',
    ativo boolean not null default true,
    vigencia_inicio date null,
    vigencia_fim date null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.comercio_tabela_preco_item (
    id bigserial primary key,
    tabela_preco_id bigint not null references sigov.comercio_tabela_preco(id),
    produto_id bigint not null references sigov.comercio_produto(id),
    preco numeric(14,2) not null,
    desconto_maximo_percentual numeric(8,4) not null default 0,
    unique(tabela_preco_id, produto_id)
);

create table if not exists sigov.comercio_condicao_pagamento (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    parcelas int not null default 1,
    intervalo_dias int not null default 30,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.comercio_forma_pagamento (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    tipo varchar(40) not null default 'DINHEIRO',
    gera_conta_receber boolean not null default false,
    movimenta_caixa boolean not null default true,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.comercio_orcamento (
    id bigserial primary key,
    tenant_id bigint not null,
    cliente_id bigint null references sigov.comercio_cliente(id),
    vendedor_id bigint null references sigov.comercio_vendedor(id),
    tabela_preco_id bigint null references sigov.comercio_tabela_preco(id),
    numero varchar(80) not null,
    status varchar(40) not null default 'ABERTO',
    subtotal numeric(14,2) not null default 0,
    desconto numeric(14,2) not null default 0,
    acrescimo numeric(14,2) not null default 0,
    total numeric(14,2) not null default 0,
    observacao text null,
    created_at timestamptz not null default now(),
    aprovado_at timestamptz null,
    reprovado_at timestamptz null,
    unique(tenant_id, numero)
);

create table if not exists sigov.comercio_orcamento_item (
    id bigserial primary key,
    orcamento_id bigint not null references sigov.comercio_orcamento(id),
    produto_id bigint not null references sigov.comercio_produto(id),
    descricao varchar(300) not null,
    quantidade numeric(14,4) not null,
    valor_unitario numeric(14,2) not null,
    desconto numeric(14,2) not null default 0,
    total numeric(14,2) not null
);

create table if not exists sigov.comercio_pedido (
    id bigserial primary key,
    tenant_id bigint not null,
    cliente_id bigint null references sigov.comercio_cliente(id),
    vendedor_id bigint null references sigov.comercio_vendedor(id),
    representante_id bigint null references sigov.comercio_representante(id),
    tabela_preco_id bigint null references sigov.comercio_tabela_preco(id),
    orcamento_id bigint null references sigov.comercio_orcamento(id),
    numero varchar(80) not null,
    status varchar(40) not null default 'ABERTO',
    subtotal numeric(14,2) not null default 0,
    desconto numeric(14,2) not null default 0,
    acrescimo numeric(14,2) not null default 0,
    total numeric(14,2) not null default 0,
    observacao text null,
    estoque_reservado boolean not null default false,
    estoque_baixado boolean not null default false,
    created_at timestamptz not null default now(),
    confirmado_at timestamptz null,
    separado_at timestamptz null,
    faturado_at timestamptz null,
    cancelado_at timestamptz null,
    unique(tenant_id, numero)
);

create table if not exists sigov.comercio_pedido_item (
    id bigserial primary key,
    pedido_id bigint not null references sigov.comercio_pedido(id),
    produto_id bigint not null references sigov.comercio_produto(id),
    descricao varchar(300) not null,
    quantidade numeric(14,4) not null,
    valor_unitario numeric(14,2) not null,
    desconto numeric(14,2) not null default 0,
    total numeric(14,2) not null,
    gera_os boolean not null default false
);

create table if not exists sigov.comercio_caixa (
    id bigserial primary key,
    tenant_id bigint not null,
    usuario_abertura_id bigint null,
    usuario_fechamento_id bigint null,
    status varchar(40) not null default 'ABERTO',
    valor_abertura numeric(14,2) not null default 0,
    valor_fechamento numeric(14,2) null,
    aberto_at timestamptz not null default now(),
    fechado_at timestamptz null,
    observacao text null
);

create table if not exists sigov.comercio_venda (
    id bigserial primary key,
    tenant_id bigint not null,
    caixa_id bigint null references sigov.comercio_caixa(id),
    cliente_id bigint null references sigov.comercio_cliente(id),
    vendedor_id bigint null references sigov.comercio_vendedor(id),
    numero varchar(80) not null,
    tipo varchar(40) not null default 'BALCAO',
    status varchar(40) not null default 'ABERTA',
    subtotal numeric(14,2) not null default 0,
    desconto numeric(14,2) not null default 0,
    acrescimo numeric(14,2) not null default 0,
    total numeric(14,2) not null default 0,
    observacao text null,
    estoque_baixado boolean not null default false,
    created_at timestamptz not null default now(),
    finalizada_at timestamptz null,
    cancelada_at timestamptz null,
    unique(tenant_id, numero)
);

create table if not exists sigov.comercio_venda_item (
    id bigserial primary key,
    venda_id bigint not null references sigov.comercio_venda(id),
    produto_id bigint not null references sigov.comercio_produto(id),
    descricao varchar(300) not null,
    quantidade numeric(14,4) not null,
    valor_unitario numeric(14,2) not null,
    desconto numeric(14,2) not null default 0,
    total numeric(14,2) not null
);

create table if not exists sigov.comercio_caixa_movimento (
    id bigserial primary key,
    tenant_id bigint not null,
    caixa_id bigint not null references sigov.comercio_caixa(id),
    tipo varchar(40) not null,
    forma_pagamento_id bigint null references sigov.comercio_forma_pagamento(id),
    valor numeric(14,2) not null,
    observacao text null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.comercio_recebimento (
    id bigserial primary key,
    tenant_id bigint not null,
    venda_id bigint null references sigov.comercio_venda(id),
    pedido_id bigint null references sigov.comercio_pedido(id),
    forma_pagamento_id bigint null references sigov.comercio_forma_pagamento(id),
    valor numeric(14,2) not null,
    status varchar(40) not null default 'PENDENTE',
    vencimento date null,
    recebido_at timestamptz null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.comercio_comissao (
    id bigserial primary key,
    tenant_id bigint not null,
    venda_id bigint null references sigov.comercio_venda(id),
    pedido_id bigint null references sigov.comercio_pedido(id),
    vendedor_id bigint null references sigov.comercio_vendedor(id),
    representante_id bigint null references sigov.comercio_representante(id),
    base_calculo numeric(14,2) not null,
    percentual numeric(8,4) not null,
    valor numeric(14,2) not null,
    status varchar(40) not null default 'PENDENTE',
    created_at timestamptz not null default now(),
    paga_at timestamptz null
);

create table if not exists sigov.comercio_separacao (
    id bigserial primary key,
    tenant_id bigint not null,
    pedido_id bigint not null references sigov.comercio_pedido(id),
    status varchar(40) not null default 'ABERTA',
    responsavel_id bigint null,
    created_at timestamptz not null default now(),
    conferida_at timestamptz null,
    unique(tenant_id, pedido_id)
);

create table if not exists sigov.comercio_separacao_item (
    id bigserial primary key,
    separacao_id bigint not null references sigov.comercio_separacao(id),
    pedido_item_id bigint not null references sigov.comercio_pedido_item(id),
    produto_id bigint not null references sigov.comercio_produto(id),
    quantidade_solicitada numeric(14,4) not null,
    quantidade_separada numeric(14,4) not null default 0,
    quantidade_conferida numeric(14,4) not null default 0,
    status varchar(40) not null default 'PENDENTE'
);

create table if not exists sigov.financeiro_conta_receber (
    id bigserial primary key,
    tenant_id bigint not null,
    origem varchar(80) not null,
    origem_id bigint not null,
    cliente_id bigint null,
    numero_documento varchar(80) null,
    parcela int not null default 1,
    valor_original numeric(14,2) not null,
    valor_aberto numeric(14,2) not null,
    vencimento date not null,
    status varchar(40) not null default 'ABERTA',
    created_at timestamptz not null default now(),
    recebido_at timestamptz null
);

create table if not exists sigov.comercio_estoque_saldo (
    id bigserial primary key,
    tenant_id bigint not null,
    produto_id bigint not null references sigov.comercio_produto(id),
    saldo numeric(14,4) not null default 0,
    reservado numeric(14,4) not null default 0,
    updated_at timestamptz null,
    unique(tenant_id, produto_id)
);

create table if not exists sigov.comercio_estoque_movimento (
    id bigserial primary key,
    tenant_id bigint not null,
    produto_id bigint not null references sigov.comercio_produto(id),
    origem varchar(80) not null,
    origem_id bigint not null,
    tipo varchar(80) not null,
    quantidade numeric(14,4) not null,
    saldo_anterior numeric(14,4) null,
    saldo_posterior numeric(14,4) null,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create index if not exists ix_comercio_cliente_tenant on sigov.comercio_cliente(tenant_id);
create index if not exists ix_comercio_cliente_documento on sigov.comercio_cliente(tenant_id, documento);
create index if not exists ix_comercio_produto_tenant on sigov.comercio_produto(tenant_id);
create index if not exists ix_comercio_produto_codigo on sigov.comercio_produto(tenant_id, codigo);
create index if not exists ix_comercio_produto_codigo_barras on sigov.comercio_produto(tenant_id, codigo_barras);
create index if not exists ix_comercio_orcamento_status_data on sigov.comercio_orcamento(tenant_id, status, created_at);
create index if not exists ix_comercio_orcamento_cliente on sigov.comercio_orcamento(tenant_id, cliente_id);
create index if not exists ix_comercio_pedido_status_data on sigov.comercio_pedido(tenant_id, status, created_at);
create index if not exists ix_comercio_pedido_cliente on sigov.comercio_pedido(tenant_id, cliente_id);
create index if not exists ix_comercio_pedido_vendedor on sigov.comercio_pedido(tenant_id, vendedor_id);
create index if not exists ix_comercio_venda_status_data on sigov.comercio_venda(tenant_id, status, created_at);
create index if not exists ix_comercio_venda_numero on sigov.comercio_venda(tenant_id, numero);
create index if not exists ix_comercio_venda_cliente on sigov.comercio_venda(tenant_id, cliente_id);
create index if not exists ix_comercio_venda_vendedor on sigov.comercio_venda(tenant_id, vendedor_id);
create index if not exists ix_comercio_venda_item_produto on sigov.comercio_venda_item(produto_id);
create index if not exists ix_comercio_caixa_status_data on sigov.comercio_caixa(tenant_id, status, aberto_at);
create index if not exists ix_comercio_recebimento_status_data on sigov.comercio_recebimento(tenant_id, status, created_at);
create index if not exists ix_financeiro_conta_receber_status_vencimento on sigov.financeiro_conta_receber(tenant_id, status, vencimento);
create index if not exists ix_financeiro_conta_receber_cliente on sigov.financeiro_conta_receber(tenant_id, cliente_id);
create index if not exists ix_comercio_separacao_status on sigov.comercio_separacao(tenant_id, status);
create index if not exists ix_comercio_comissao_status on sigov.comercio_comissao(tenant_id, status);
create index if not exists ix_comercio_estoque_movimento_produto_data on sigov.comercio_estoque_movimento(tenant_id, produto_id, created_at);

insert into sigov.permissao (modulo,recurso,acao,chave,descricao,ativo) values
('comercio','dashboard','visualizar','comercio.dashboard.visualizar','Visualizar dashboard comercial avançado',true),
('comercio','clientes','visualizar','comercio.clientes.visualizar','Visualizar clientes comerciais',true),
('comercio','clientes','criar','comercio.clientes.criar','Criar clientes comerciais',true),
('comercio','clientes','editar','comercio.clientes.editar','Editar clientes comerciais',true),
('comercio','produtos','visualizar','comercio.produtos.visualizar','Visualizar produtos comerciais',true),
('comercio','produtos','criar','comercio.produtos.criar','Criar produtos comerciais',true),
('comercio','produtos','editar','comercio.produtos.editar','Editar produtos comerciais',true),
('comercio','orcamentos','visualizar','comercio.orcamentos.visualizar','Visualizar orçamentos',true),
('comercio','orcamentos','criar','comercio.orcamentos.criar','Criar orçamentos',true),
('comercio','orcamentos','aprovar','comercio.orcamentos.aprovar','Aprovar orçamentos',true),
('comercio','pedidos','visualizar','comercio.pedidos.visualizar','Visualizar pedidos',true),
('comercio','pedidos','criar','comercio.pedidos.criar','Criar pedidos',true),
('comercio','pedidos','confirmar','comercio.pedidos.confirmar','Confirmar pedidos',true),
('comercio','pedidos','cancelar','comercio.pedidos.cancelar','Cancelar pedidos',true),
('comercio','pdv','acessar','comercio.pdv.acessar','Acessar PDV',true),
('comercio','vendas','criar','comercio.vendas.criar','Criar vendas',true),
('comercio','vendas','finalizar','comercio.vendas.finalizar','Finalizar vendas',true),
('comercio','vendas','cancelar','comercio.vendas.cancelar','Cancelar vendas',true),
('comercio','caixa','abrir','comercio.caixa.abrir','Abrir caixa',true),
('comercio','caixa','fechar','comercio.caixa.fechar','Fechar caixa',true),
('comercio','caixa','suprimento','comercio.caixa.suprimento','Registrar suprimento',true),
('comercio','caixa','sangria','comercio.caixa.sangria','Registrar sangria',true),
('comercio','tabelas','visualizar','comercio.tabelas.visualizar','Visualizar tabelas de preço',true),
('comercio','tabelas','editar','comercio.tabelas.editar','Editar tabelas de preço',true),
('comercio','comissoes','visualizar','comercio.comissoes.visualizar','Visualizar comissões',true),
('comercio','comissoes','calcular','comercio.comissoes.calcular','Calcular comissões',true),
('comercio','estoque','vender_negativo','comercio.estoque.vender_negativo','Permitir venda com estoque negativo',true),
('comercio','venda','desconto_especial','comercio.venda.desconto_especial','Permitir desconto acima do limite',true),
('financeiro','contas_receber','visualizar','financeiro.contas_receber.visualizar','Visualizar contas a receber comerciais',true),
('financeiro','contas_receber','receber','financeiro.contas_receber.receber','Receber contas a receber comerciais',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.ativo=true and p.is_deleted=false and (p.modulo in ('comercio','financeiro') or p.chave like 'financeiro.contas_receber.%')
where pa.ativo=true and pa.is_deleted=false
  and (coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('ADMIN_GERAL','ADMINISTRADOR_GERAL','ADMIN_TENANT','ADMINISTRADOR_TENANT','GERENTE_COMERCIAL') or upper(pa.nome) like '%ADMIN%')
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.chave in ('comercio.pdv.acessar','comercio.vendas.criar','comercio.vendas.finalizar','comercio.caixa.abrir','comercio.caixa.fechar','comercio.caixa.suprimento','comercio.caixa.sangria')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('OPERADOR_CAIXA','CAIXA')
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma' order by id limit 1) t
join sigov.permissao p on p.chave in ('comercio.clientes.visualizar','comercio.clientes.criar','comercio.clientes.editar','comercio.orcamentos.visualizar','comercio.orcamentos.criar','comercio.pedidos.visualizar','comercio.pedidos.criar','comercio.vendas.criar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('VENDEDOR','REPRESENTANTE')
and not exists (select 1 from sigov.perfil_permissao pp where pp.tenant_id = coalesce(pa.tenant_id, t.id) and pp.perfil_acesso_id = pa.id and pp.permissao_id = p.id);

insert into sigov.tenant_modulo_pacote (codigo, nome, descricao, modulos_json) values
('COMERCIO_STARTER','Comércio Starter','Varejo com PDV, caixa e estoque.','["comercial","comercio_varejo","pdv","caixa","estoque_compras"]'::jsonb),
('COMERCIO_PLUS','Comércio Plus','Varejo e atacado integrados ao estoque e financeiro inicial.','["comercial","comercio_varejo","comercio_atacado","pdv","caixa","estoque_compras","financeiro_empresarial"]'::jsonb),
('ATACADO_PRO','Atacado Pro','Pedidos, separação e financeiro inicial para atacado.','["comercial","comercio_atacado","pedidos","estoque_compras","financeiro_empresarial"]'::jsonb),
('BUSINESS_FULL','Business Full','Comércio, OS, manutenção industrial, estoque e financeiro inicial integrados.','["comercial","comercio_varejo","comercio_atacado","pdv","caixa","estoque_compras","ordem_servico","manutencao_industrial","financeiro_empresarial"]'::jsonb)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, modulos_json=excluded.modulos_json, ativo=true;

insert into sigov.saas_plano (codigo,nome,descricao,tipo_plano,preco_base,moeda,periodicidade,limite_usuarios,publico,destaque,ativo,ordem) values
('COMERCIO_STARTER','Comércio Starter','Plano inicial de varejo com PDV, caixa e estoque.','MENSAL',0,'BRL','MENSAL',20,true,true,true,50),
('COMERCIO_PLUS','Comércio Plus','Plano integrado para varejo e atacado com financeiro comercial inicial.','MENSAL',0,'BRL','MENSAL',60,true,true,true,60),
('ATACADO_PRO','Atacado Pro','Plano atacadista com pedidos, separação, estoque e contas a receber.','MENSAL',0,'BRL','MENSAL',80,true,false,true,70),
('BUSINESS_FULL','Business Full','Plano empresarial completo com comércio, OS, manutenção, estoque e financeiro inicial.','MENSAL',0,'BRL','MENSAL',200,false,false,true,80)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, limite_usuarios=excluded.limite_usuarios, publico=excluded.publico, destaque=excluded.destaque, ativo=true, updated_at=now();

with plano_modulos(codigo, modulo) as (
    values
    ('COMERCIO_STARTER','comercial'),('COMERCIO_STARTER','comercio_varejo'),('COMERCIO_STARTER','pdv'),('COMERCIO_STARTER','caixa'),('COMERCIO_STARTER','estoque_compras'),
    ('COMERCIO_PLUS','comercial'),('COMERCIO_PLUS','comercio_varejo'),('COMERCIO_PLUS','comercio_atacado'),('COMERCIO_PLUS','pdv'),('COMERCIO_PLUS','caixa'),('COMERCIO_PLUS','estoque_compras'),('COMERCIO_PLUS','financeiro_empresarial'),
    ('ATACADO_PRO','comercial'),('ATACADO_PRO','comercio_atacado'),('ATACADO_PRO','pedidos'),('ATACADO_PRO','estoque_compras'),('ATACADO_PRO','financeiro_empresarial'),
    ('BUSINESS_FULL','comercial'),('BUSINESS_FULL','comercio_varejo'),('BUSINESS_FULL','comercio_atacado'),('BUSINESS_FULL','pdv'),('BUSINESS_FULL','caixa'),('BUSINESS_FULL','estoque_compras'),('BUSINESS_FULL','ordem_servico'),('BUSINESS_FULL','manutencao_industrial'),('BUSINESS_FULL','financeiro_empresarial')
)
insert into sigov.saas_plano_modulo (plano_id, modulo_codigo, incluso)
select p.id, pm.modulo, true from plano_modulos pm join sigov.saas_plano p on p.codigo=pm.codigo
on conflict (plano_id, modulo_codigo) do update set incluso=true;
