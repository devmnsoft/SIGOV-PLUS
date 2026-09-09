-- Compatibilidade forward-only entre a unidade consumidora do saneamento base
-- e o contrato comercial de FUNC07. O identificador funcional legado
-- `codigo_unidade` permanece preservado e passa a alimentar `matricula`.
alter table if exists sigov.saneamento_unidade_consumidora
    add column if not exists matricula varchar(40),
    add column if not exists proprietario_id bigint,
    add column if not exists inquilino_id bigint,
    add column if not exists responsavel_financeiro_id bigint,
    add column if not exists orgao_pagador_id bigint,
    add column if not exists logradouro varchar(250),
    add column if not exists numero varchar(30),
    add column if not exists complemento varchar(120),
    add column if not exists cep varchar(8),
    add column if not exists cidade varchar(120),
    add column if not exists uf char(2),
    add column if not exists categoria varchar(30),
    add column if not exists subcategoria varchar(60),
    add column if not exists moradores integer not null default 0,
    add column if not exists economias integer not null default 1,
    add column if not exists area_edificada numeric(12,2),
    add column if not exists inscricao_imobiliaria varchar(60),
    add column if not exists reservatorio boolean not null default false,
    add column if not exists cisterna boolean not null default false,
    add column if not exists fonte_propria boolean not null default false,
    add column if not exists fossa boolean not null default false;

update sigov.saneamento_unidade_consumidora
set matricula = coalesce(nullif(matricula, ''), left(codigo_unidade, 40)),
    proprietario_id = coalesce(proprietario_id, consumidor_id),
    logradouro = coalesce(nullif(logradouro, ''), nullif(endereco_json->>'logradouro', ''), 'Não informado'),
    numero = coalesce(nullif(numero, ''), nullif(endereco_json->>'numero', ''), 'S/N'),
    bairro = coalesce(nullif(bairro, ''), nullif(endereco_json->>'bairro', ''), 'Não informado'),
    cidade = coalesce(nullif(cidade, ''), nullif(endereco_json->>'cidade', ''), 'Não informado'),
    uf = coalesce(nullif(uf, ''), nullif(upper(left(endereco_json->>'uf', 2)), ''), 'NA'),
    categoria = coalesce(nullif(categoria, ''), 'RESIDENCIAL')
where matricula is null or proprietario_id is null or logradouro is null or numero is null
   or bairro is null or cidade is null or uf is null or categoria is null;

alter table if exists sigov.saneamento_unidade_consumidora
    alter column matricula set not null,
    alter column proprietario_id set not null,
    alter column logradouro set not null,
    alter column numero set not null,
    alter column bairro set not null,
    alter column cidade set not null,
    alter column uf set not null,
    alter column categoria set not null;

create unique index if not exists ux_san_uc_matricula
    on sigov.saneamento_unidade_consumidora(tenant_id, entidade_id, matricula);
