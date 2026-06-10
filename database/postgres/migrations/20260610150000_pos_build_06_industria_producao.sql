-- SIGOV Pós-Build 06: Indústria e Produção avançada, chão de fábrica e integrações.
-- Migration idempotente: CREATE TABLE/INDEX IF NOT EXISTS e seeds com ON CONFLICT, sem remoção destrutiva.
create schema if not exists sigov;
create extension if not exists pgcrypto;

create table if not exists sigov.industria_centro_trabalho (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    descricao text null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.industria_recurso (
    id bigserial primary key,
    tenant_id bigint not null,
    centro_trabalho_id bigint null references sigov.industria_centro_trabalho(id),
    codigo varchar(80) not null,
    nome varchar(200) not null,
    tipo varchar(40) not null,
    custo_hora numeric(14,4) null,
    capacidade_hora numeric(14,4) null,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo),
    constraint ck_industria_recurso_tipo check (tipo in ('MAQUINA','OPERADOR','FERRAMENTA','LINHA','CELULA'))
);

create table if not exists sigov.industria_produto (
    id bigserial primary key,
    tenant_id bigint not null,
    produto_id bigint null,
    codigo varchar(80) not null,
    nome varchar(200) not null,
    tipo varchar(40) not null default 'ACABADO',
    unidade varchar(20) not null default 'UN',
    controla_lote boolean not null default false,
    controla_validade boolean not null default false,
    exige_ficha_tecnica boolean not null default true,
    inspecao_obrigatoria boolean not null default false,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo),
    constraint ck_industria_produto_tipo check (tipo in ('MATERIA_PRIMA','SEMI_ACABADO','ACABADO','EMBALAGEM','INSUMO'))
);

create table if not exists sigov.industria_ficha_tecnica (
    id bigserial primary key,
    tenant_id bigint not null,
    produto_id bigint not null references sigov.industria_produto(id),
    codigo varchar(80) not null,
    versao varchar(20) not null default '1',
    status varchar(40) not null default 'ATIVA',
    rendimento numeric(14,4) not null default 1,
    observacao text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo, versao)
);

create table if not exists sigov.industria_ficha_tecnica_item (
    id bigserial primary key,
    ficha_tecnica_id bigint not null references sigov.industria_ficha_tecnica(id),
    componente_produto_id bigint not null references sigov.industria_produto(id),
    quantidade numeric(14,6) not null,
    perda_percentual numeric(7,4) not null default 0,
    unidade varchar(20) not null default 'UN',
    obrigatorio boolean not null default true,
    ordem int not null default 0
);

create table if not exists sigov.industria_roteiro (
    id bigserial primary key,
    tenant_id bigint not null,
    produto_id bigint not null references sigov.industria_produto(id),
    codigo varchar(80) not null,
    nome varchar(200) not null,
    versao varchar(20) not null default '1',
    status varchar(40) not null default 'ATIVO',
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo, versao)
);

create table if not exists sigov.industria_roteiro_operacao (
    id bigserial primary key,
    roteiro_id bigint not null references sigov.industria_roteiro(id),
    centro_trabalho_id bigint null references sigov.industria_centro_trabalho(id),
    recurso_id bigint null references sigov.industria_recurso(id),
    codigo varchar(80) not null,
    descricao varchar(300) not null,
    tempo_setup_min numeric(14,4) not null default 0,
    tempo_execucao_min numeric(14,4) not null default 0,
    ordem int not null default 0
);

create table if not exists sigov.industria_ordem_producao (
    id bigserial primary key,
    tenant_id bigint not null,
    numero varchar(80) not null,
    produto_id bigint not null references sigov.industria_produto(id),
    ficha_tecnica_id bigint null references sigov.industria_ficha_tecnica(id),
    roteiro_id bigint null references sigov.industria_roteiro(id),
    pedido_id bigint null,
    os_id bigint null,
    status varchar(40) not null default 'PLANEJADA',
    quantidade_planejada numeric(14,4) not null,
    quantidade_produzida numeric(14,4) not null default 0,
    quantidade_refugada numeric(14,4) not null default 0,
    data_previsao_inicio timestamptz null,
    data_previsao_fim timestamptz null,
    inicio_at timestamptz null,
    fim_at timestamptz null,
    observacao text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, numero),
    constraint ck_industria_op_status check (status in ('PLANEJADA','LIBERADA','EM_PRODUCAO','PAUSADA','CONCLUIDA','CANCELADA'))
);

create table if not exists sigov.industria_ordem_material (
    id bigserial primary key,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    produto_id bigint not null references sigov.industria_produto(id),
    quantidade_planejada numeric(14,6) not null,
    quantidade_consumida numeric(14,6) not null default 0,
    unidade varchar(20) not null default 'UN'
);

create table if not exists sigov.industria_ordem_operacao (
    id bigserial primary key,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    operacao_codigo varchar(80) not null,
    descricao varchar(300) not null,
    centro_trabalho_id bigint null references sigov.industria_centro_trabalho(id),
    recurso_id bigint null references sigov.industria_recurso(id),
    status varchar(40) not null default 'PENDENTE',
    inicio_at timestamptz null,
    fim_at timestamptz null,
    ordem int not null default 0
);

create table if not exists sigov.industria_apontamento (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    ordem_operacao_id bigint null references sigov.industria_ordem_operacao(id),
    usuario_id bigint null,
    tipo varchar(40) not null,
    origem varchar(80) not null default 'CHAO_FABRICA',
    inicio_at timestamptz not null,
    fim_at timestamptz null,
    quantidade_boas numeric(14,4) not null default 0,
    quantidade_refugo numeric(14,4) not null default 0,
    observacao text null,
    created_at timestamptz not null default now(),
    constraint ck_industria_apontamento_tipo check (tipo in ('INICIO','PAUSA','RETOMADA','PRODUCAO','FINALIZACAO'))
);

create table if not exists sigov.industria_consumo_material (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    produto_id bigint not null,
    almoxarifado_id bigint null,
    quantidade numeric(14,6) not null,
    custo_unitario numeric(14,6) null,
    origem varchar(80) not null default 'OP',
    usuario_id bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.industria_producao_acabada (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    produto_id bigint not null,
    almoxarifado_id bigint null,
    quantidade numeric(14,4) not null,
    lote varchar(80) null,
    validade date null,
    usuario_id bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.industria_refugo (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    produto_id bigint null,
    quantidade numeric(14,4) not null,
    motivo varchar(200) null,
    causa varchar(200) null,
    usuario_id bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.industria_inspecao_qualidade (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    produto_id bigint null,
    status varchar(40) not null default 'PENDENTE',
    resultado varchar(40) null,
    observacao text null,
    inspecionado_por bigint null,
    inspecionado_at timestamptz null,
    created_at timestamptz not null default now(),
    constraint ck_industria_inspecao_resultado check (resultado is null or resultado in ('APROVADO','REPROVADO','APROVADO_COM_RESTRICAO'))
);

create table if not exists sigov.industria_parada_producao (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint null references sigov.industria_ordem_producao(id),
    recurso_id bigint null references sigov.industria_recurso(id),
    motivo varchar(200) not null,
    inicio_at timestamptz not null,
    fim_at timestamptz null,
    impacto_minutos numeric(14,4) null,
    gerou_os boolean not null default false,
    os_id bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.industria_custo_ordem (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    custo_material numeric(14,4) not null default 0,
    custo_mao_obra numeric(14,4) not null default 0,
    custo_maquina numeric(14,4) not null default 0,
    custo_indireto numeric(14,4) not null default 0,
    custo_refugo numeric(14,4) not null default 0,
    custo_total numeric(14,4) not null default 0,
    custo_unitario numeric(14,6) null,
    calculado_at timestamptz not null default now()
);

create table if not exists sigov.industria_ordem_historico (
    id bigserial primary key,
    tenant_id bigint not null,
    ordem_id bigint not null references sigov.industria_ordem_producao(id),
    status_anterior varchar(40) null,
    status_novo varchar(40) not null,
    usuario_id bigint null,
    origem varchar(80) not null default 'API',
    observacao text null,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create index if not exists ix_industria_centro_tenant on sigov.industria_centro_trabalho(tenant_id, ativo);
create index if not exists ix_industria_recurso_tenant on sigov.industria_recurso(tenant_id, tipo, ativo);
create index if not exists ix_industria_produto_tenant on sigov.industria_produto(tenant_id, tipo, ativo);
create index if not exists ix_industria_ficha_produto on sigov.industria_ficha_tecnica(tenant_id, produto_id, status);
create index if not exists ix_industria_roteiro_produto on sigov.industria_roteiro(tenant_id, produto_id, status);
create index if not exists ix_industria_op_status on sigov.industria_ordem_producao(tenant_id, status, created_at);
create index if not exists ix_industria_op_pedido on sigov.industria_ordem_producao(tenant_id, pedido_id);
create index if not exists ix_industria_apontamento_ordem on sigov.industria_apontamento(tenant_id, ordem_id, created_at);
create index if not exists ix_industria_consumo_ordem on sigov.industria_consumo_material(tenant_id, ordem_id, created_at);
create index if not exists ix_industria_producao_ordem on sigov.industria_producao_acabada(tenant_id, ordem_id, created_at);
create index if not exists ix_industria_refugo_ordem on sigov.industria_refugo(tenant_id, ordem_id, created_at);
create index if not exists ix_industria_inspecao_status on sigov.industria_inspecao_qualidade(tenant_id, status, created_at);
create index if not exists ix_industria_parada_recurso on sigov.industria_parada_producao(tenant_id, recurso_id, inicio_at);
create index if not exists ix_industria_historico_ordem on sigov.industria_ordem_historico(tenant_id, ordem_id, created_at);

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ativo) values
('industria_producao','Indústria e Produção','Produção por ordem, BOM, roteiro, chão de fábrica, qualidade e custos.','EMPRESARIAL',true),
('manutencao_industrial','Manutenção Industrial','Ativos, planos e manutenção industrial integrada.','EMPRESARIAL',true),
('ordem_servico','Ordem de Serviço','Ordens de serviço técnicas e operacionais.','EMPRESARIAL',true),
('estoque_compras','Estoque e Compras','Saldos, movimentos, compras e almoxarifado.','EMPRESARIAL',true),
('comercial','Comercial','Clientes, pedidos, vendas e CRM comercial.','EMPRESARIAL',true),
('comercio_varejo','Comércio Varejo','Varejo, balcão e operações de loja.','EMPRESARIAL',true),
('comercio_atacado','Comércio Atacado','Pedidos, tabelas e separação atacadista.','EMPRESARIAL',true),
('financeiro_empresarial','Financeiro Empresarial','Contas, caixa e financeiro empresarial.','EMPRESARIAL',true)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, categoria=excluded.categoria, ativo=true;

insert into sigov.tenant_modulo_pacote (codigo, nome, descricao, modulos_json) values
('INDUSTRIAL_STARTER','Industrial Starter','Produção, estoque e ordem de serviço para iniciar operação industrial.','["industria_producao","estoque_compras","ordem_servico"]'::jsonb),
('INDUSTRIAL_PLUS','Industrial Plus','Produção integrada a manutenção, OS, compras, estoque e financeiro.','["industria_producao","manutencao_industrial","ordem_servico","estoque_compras","compras","financeiro_empresarial"]'::jsonb),
('FACTORY_FULL','Factory Full','Pacote fabril completo com comercial atacadista, produção, estoque e financeiro.','["industria_producao","manutencao_industrial","ordem_servico","estoque_compras","comercial","comercio_atacado","financeiro_empresarial"]'::jsonb),
('BUSINESS_FULL','Business Full','Pacote empresarial completo com comércio, produção, OS, manutenção, estoque e financeiro.','["comercial","comercio_varejo","comercio_atacado","pdv","caixa","estoque_compras","ordem_servico","manutencao_industrial","industria_producao","financeiro_empresarial"]'::jsonb)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, modulos_json=excluded.modulos_json, ativo=true;

insert into sigov.saas_plano (codigo,nome,descricao,tipo_plano,preco_base,moeda,periodicidade,limite_usuarios,publico,destaque,ativo,ordem) values
('INDUSTRIAL_STARTER','Industrial Starter','Indústria por ordem com estoque e OS.','MENSAL',0,'BRL','MENSAL',40,true,true,true,90),
('INDUSTRIAL_PLUS','Industrial Plus','Indústria integrada com manutenção, compras e financeiro.','MENSAL',0,'BRL','MENSAL',120,true,true,true,100),
('FACTORY_FULL','Factory Full','Operação fabril e comercial atacadista completa.','MENSAL',0,'BRL','MENSAL',180,true,false,true,110),
('BUSINESS_FULL','Business Full','Empresa completa: comercial, estoque, OS, manutenção, indústria e financeiro.','MENSAL',0,'BRL','MENSAL',250,false,false,true,120)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, limite_usuarios=excluded.limite_usuarios, publico=excluded.publico, destaque=excluded.destaque, ativo=true, updated_at=now();

with plano_modulos(codigo, modulo) as (
    values
    ('INDUSTRIAL_STARTER','industria_producao'),('INDUSTRIAL_STARTER','estoque_compras'),('INDUSTRIAL_STARTER','ordem_servico'),
    ('INDUSTRIAL_PLUS','industria_producao'),('INDUSTRIAL_PLUS','manutencao_industrial'),('INDUSTRIAL_PLUS','ordem_servico'),('INDUSTRIAL_PLUS','estoque_compras'),('INDUSTRIAL_PLUS','compras'),('INDUSTRIAL_PLUS','financeiro_empresarial'),
    ('FACTORY_FULL','industria_producao'),('FACTORY_FULL','manutencao_industrial'),('FACTORY_FULL','ordem_servico'),('FACTORY_FULL','estoque_compras'),('FACTORY_FULL','comercial'),('FACTORY_FULL','comercio_atacado'),('FACTORY_FULL','financeiro_empresarial'),
    ('BUSINESS_FULL','comercial'),('BUSINESS_FULL','comercio_varejo'),('BUSINESS_FULL','comercio_atacado'),('BUSINESS_FULL','pdv'),('BUSINESS_FULL','caixa'),('BUSINESS_FULL','estoque_compras'),('BUSINESS_FULL','ordem_servico'),('BUSINESS_FULL','manutencao_industrial'),('BUSINESS_FULL','industria_producao'),('BUSINESS_FULL','financeiro_empresarial')
)
insert into sigov.saas_plano_modulo (plano_id, modulo_codigo, incluso)
select p.id, pm.modulo, true from plano_modulos pm join sigov.saas_plano p on p.codigo=pm.codigo
on conflict (plano_id, modulo_codigo) do update set incluso=true;

insert into sigov.perfil_acesso (nome, descricao, codigo_externo, ativo) values
('Gerente Industrial','Gerencia produção, chão de fábrica, custos e indicadores.','GERENTE_INDUSTRIAL',true),
('PCP','Planeja e libera ordens de produção.','PCP',true),
('Operador de Produção','Realiza apontamentos de chão de fábrica.','OPERADOR_PRODUCAO',true),
('Qualidade','Executa inspeções e liberações de qualidade.','QUALIDADE',true),
('Manutenção','Atua em paradas e OS de manutenção.','MANUTENCAO',true)
on conflict do nothing;

insert into sigov.permissao (modulo,recurso,acao,chave,descricao,ativo) values
('industria','dashboard','visualizar','industria.dashboard.visualizar','Visualizar dashboard industrial',true),
('industria','centros','visualizar','industria.centros.visualizar','Visualizar centros de trabalho',true),
('industria','centros','criar','industria.centros.criar','Criar centros de trabalho',true),
('industria','centros','editar','industria.centros.editar','Editar centros de trabalho',true),
('industria','recursos','visualizar','industria.recursos.visualizar','Visualizar recursos produtivos',true),
('industria','recursos','criar','industria.recursos.criar','Criar recursos produtivos',true),
('industria','recursos','editar','industria.recursos.editar','Editar recursos produtivos',true),
('industria','produtos','visualizar','industria.produtos.visualizar','Visualizar produtos industriais',true),
('industria','produtos','criar','industria.produtos.criar','Criar produtos industriais',true),
('industria','produtos','editar','industria.produtos.editar','Editar produtos industriais',true),
('industria','fichas','visualizar','industria.fichas.visualizar','Visualizar fichas técnicas',true),
('industria','fichas','criar','industria.fichas.criar','Criar fichas técnicas',true),
('industria','fichas','editar','industria.fichas.editar','Editar fichas técnicas',true),
('industria','roteiros','visualizar','industria.roteiros.visualizar','Visualizar roteiros',true),
('industria','roteiros','criar','industria.roteiros.criar','Criar roteiros',true),
('industria','roteiros','editar','industria.roteiros.editar','Editar roteiros',true),
('industria','ordens','visualizar','industria.ordens.visualizar','Visualizar ordens de produção',true),
('industria','ordens','criar','industria.ordens.criar','Criar ordens de produção',true),
('industria','ordens','liberar','industria.ordens.liberar','Liberar ordens de produção',true),
('industria','ordens','iniciar','industria.ordens.iniciar','Iniciar ordens de produção',true),
('industria','ordens','concluir','industria.ordens.concluir','Concluir ordens de produção',true),
('industria','apontamentos','criar','industria.apontamentos.criar','Registrar apontamentos',true),
('industria','materiais','consumir','industria.materiais.consumir','Consumir material',true),
('industria','producao','registrar','industria.producao.registrar','Registrar produção acabada',true),
('industria','refugo','registrar','industria.refugo.registrar','Registrar refugo',true),
('industria','qualidade','visualizar','industria.qualidade.visualizar','Visualizar qualidade',true),
('industria','qualidade','inspecionar','industria.qualidade.inspecionar','Inspecionar qualidade',true),
('industria','paradas','visualizar','industria.paradas.visualizar','Visualizar paradas',true),
('industria','paradas','criar','industria.paradas.criar','Criar paradas',true),
('industria','custos','visualizar','industria.custos.visualizar','Visualizar custos',true),
('industria','custos','calcular','industria.custos.calcular','Calcular custos',true),
('industria','chao_fabrica','acessar','industria.chao_fabrica.acessar','Acessar chão de fábrica',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
select pa.id, p.id
from sigov.perfil_acesso pa
join sigov.permissao p on p.modulo='industria' and p.ativo=true and p.is_deleted=false
where pa.ativo=true and pa.is_deleted=false and (coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('ADMIN_GERAL','ADMINISTRADOR_GERAL','ADMIN_TENANT','ADMINISTRADOR_TENANT','GERENTE_INDUSTRIAL') or upper(pa.nome) like '%ADMIN%')
on conflict do nothing;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
select pa.id, p.id from sigov.perfil_acesso pa join sigov.permissao p on p.chave in ('industria.dashboard.visualizar','industria.ordens.visualizar','industria.ordens.criar','industria.ordens.liberar','industria.fichas.visualizar','industria.roteiros.visualizar','industria.custos.visualizar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_')))='PCP'
on conflict do nothing;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
select pa.id, p.id from sigov.perfil_acesso pa join sigov.permissao p on p.chave in ('industria.chao_fabrica.acessar','industria.ordens.visualizar','industria.ordens.iniciar','industria.apontamentos.criar','industria.materiais.consumir','industria.producao.registrar','industria.refugo.registrar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_')))='OPERADOR_PRODUCAO'
on conflict do nothing;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
select pa.id, p.id from sigov.perfil_acesso pa join sigov.permissao p on p.chave in ('industria.qualidade.visualizar','industria.qualidade.inspecionar','industria.ordens.visualizar')
where pa.ativo=true and pa.is_deleted=false and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_')))='QUALIDADE'
on conflict do nothing;

create or replace function sigov.fn_industria_set_updated_at()
returns trigger language plpgsql as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

do $$
declare r record;
begin
    for r in select unnest(array[
        'industria_centro_trabalho','industria_recurso','industria_produto','industria_ficha_tecnica',
        'industria_roteiro','industria_ordem_producao'
    ]) as table_name loop
        if not exists (
            select 1 from pg_trigger t
            join pg_class c on c.oid = t.tgrelid
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'sigov' and c.relname = r.table_name and t.tgname = format('trg_%s_updated_at', r.table_name)
        ) then
            execute format('create trigger trg_%I_updated_at before update on sigov.%I for each row execute function sigov.fn_industria_set_updated_at()', r.table_name, r.table_name);
        end if;
    end loop;
end $$;
