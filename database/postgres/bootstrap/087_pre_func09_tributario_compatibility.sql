-- Compatibilidade forward-only entre os contratos tributários históricos e
-- FUNC09. Preserva as colunas publicadas e acrescenta os nomes canônicos usados
-- pelos índices e validações atuais; nenhuma esfera de governo é presumida.
alter table if exists sigov.tributario_contribuinte
    add column if not exists entidade_id bigint,
    add column if not exists nome_razao_social varchar(250),
    add column if not exists nome_fantasia varchar(180),
    add column if not exists inscricao_municipal varchar(40),
    add column if not exists endereco jsonb not null default '{}'::jsonb,
    add column if not exists responsavel_contabil jsonb,
    add column if not exists situacao varchar(20) not null default 'ATIVO',
    add column if not exists is_deleted boolean not null default false,
    add column if not exists created_by bigint,
    add column if not exists updated_by bigint,
    add column if not exists deleted_at timestamptz,
    add column if not exists deleted_by bigint;

update sigov.tributario_contribuinte
set nome_razao_social = coalesce(nullif(nome_razao_social, ''), nome),
    inscricao_municipal = coalesce(nullif(inscricao_municipal, ''), nullif(dados_json->>'inscricao_municipal', ''))
where nome_razao_social is null or inscricao_municipal is null;

alter table if exists sigov.tributario_imovel
    add column if not exists entidade_id bigint,
    add column if not exists inscricao_imobiliaria varchar(60),
    add column if not exists proprietario_id bigint,
    add column if not exists responsavel_id bigint,
    add column if not exists situacao varchar(20) not null default 'ATIVO',
    add column if not exists valor_venal_territorial numeric(18,2) not null default 0,
    add column if not exists valor_venal_predial numeric(18,2) not null default 0,
    add column if not exists is_deleted boolean not null default false,
    add column if not exists created_by bigint,
    add column if not exists updated_by bigint,
    add column if not exists deleted_at timestamptz,
    add column if not exists deleted_by bigint;

update sigov.tributario_imovel
set inscricao_imobiliaria = coalesce(nullif(inscricao_imobiliaria, ''), left(inscricao, 60)),
    proprietario_id = coalesce(proprietario_id, contribuinte_id)
where inscricao_imobiliaria is null or proprietario_id is null;

alter table if exists sigov.tributario_lancamento
    add column if not exists numero varchar(60),
    add column if not exists vencimento date,
    add column if not exists imovel_id bigint,
    add column if not exists mobiliario_id bigint,
    add column if not exists revisao_justificativa text,
    add column if not exists cancelamento_justificativa text;

update sigov.tributario_lancamento
set numero = coalesce(nullif(numero, ''), left(codigo, 60)),
    vencimento = coalesce(vencimento, data_vencimento)
where numero is null or vencimento is null;

alter table if exists sigov.tributario_lancamento_item
    add column if not exists entidade_id bigint,
    add column if not exists ativo boolean not null default true,
    add column if not exists created_by bigint,
    add column if not exists updated_at timestamptz,
    add column if not exists updated_by bigint,
    add column if not exists deleted_at timestamptz,
    add column if not exists deleted_by bigint;

alter table if exists sigov.tributario_guia
    add column if not exists numero varchar(60),
    add column if not exists emissao_at timestamptz not null default now(),
    add column if not exists vencimento date,
    add column if not exists valor numeric(18,2),
    add column if not exists via integer not null default 1,
    add column if not exists codigo_barras varchar(100),
    add column if not exists pix_payload text;

update sigov.tributario_guia
set numero = coalesce(nullif(numero, ''), left(codigo, 60)),
    vencimento = coalesce(vencimento, data_vencimento),
    valor = coalesce(valor, valor_total)
where numero is null or vencimento is null or valor is null;

alter table if exists sigov.tributario_pagamento
    add column if not exists lote_id bigint,
    add column if not exists pago_at timestamptz,
    add column if not exists valor_pago numeric(18,2),
    add column if not exists desconto numeric(18,2) not null default 0,
    add column if not exists multa numeric(18,2) not null default 0,
    add column if not exists juros numeric(18,2) not null default 0,
    add column if not exists diferenca numeric(18,2) not null default 0,
    add column if not exists diferenca_justificativa text,
    add column if not exists estorno_justificativa text,
    add column if not exists ativo boolean not null default true,
    add column if not exists updated_at timestamptz,
    add column if not exists updated_by bigint,
    add column if not exists deleted_at timestamptz,
    add column if not exists deleted_by bigint;

update sigov.tributario_pagamento
set pago_at = coalesce(pago_at, data_pagamento::timestamptz),
    valor_pago = coalesce(valor_pago, valor)
where pago_at is null or valor_pago is null;

alter table if exists sigov.tributario_divida_ativa
    add column if not exists numero_inscricao varchar(60),
    add column if not exists livro varchar(30),
    add column if not exists folha varchar(30),
    add column if not exists inscrita_at timestamptz,
    add column if not exists valor_original numeric(18,2),
    add column if not exists valor_atualizado numeric(18,2),
    add column if not exists cancelamento_justificativa text;

update sigov.tributario_divida_ativa
set numero_inscricao = coalesce(nullif(numero_inscricao, ''), left(inscricao, 60)),
    inscrita_at = coalesce(inscrita_at, data_inscricao::timestamptz),
    valor_original = coalesce(valor_original, valor_total),
    valor_atualizado = coalesce(valor_atualizado, saldo)
where numero_inscricao is null or inscrita_at is null or valor_original is null or valor_atualizado is null;
