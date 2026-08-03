-- SIGOV+ RC38E
-- Compatibilidade entre o módulo de compras básico do Pós-Build 04 e a jornada
-- transacional completa introduzida no Pós-RC 37B.

create schema if not exists sigov;
create extension if not exists pgcrypto;

do $$
begin
    if to_regclass('sigov.compras_fornecedor') is not null then
        alter table sigov.compras_fornecedor
            add column if not exists codigo varchar(30),
            add column if not exists tipo_pessoa char(1) not null default 'J',
            add column if not exists documento_hash char(64),
            add column if not exists documento_mascarado varchar(30),
            add column if not exists razao_social varchar(200),
            add column if not exists nome_fantasia varchar(200),
            add column if not exists categoria varchar(100),
            add column if not exists porte varchar(30),
            add column if not exists condicao_pagamento varchar(100),
            add column if not exists prazo_medio integer not null default 0,
            add column if not exists observacoes text,
            add column if not exists score numeric(5,2) not null default 0,
            add column if not exists status varchar(20) not null default 'RASCUNHO',
            add column if not exists created_at timestamptz not null default now(),
            add column if not exists created_by varchar(100) not null default 'migration',
            add column if not exists updated_at timestamptz not null default now(),
            add column if not exists updated_by varchar(100) not null default 'migration',
            add column if not exists correlation_id varchar(100),
            add column if not exists version bigint not null default 1,
            add column if not exists is_deleted boolean not null default false;

        update sigov.compras_fornecedor
           set codigo = coalesce(nullif(codigo, ''), 'LEGACY-' || upper(substr(replace(id::text, '-', ''), 1, 20))),
               tipo_pessoa = case when tipo_pessoa in ('F','J') then tipo_pessoa else 'J' end,
               documento_hash = coalesce(
                   nullif(documento_hash, ''),
                   encode(digest(coalesce(nullif(documento, ''), id::text), 'sha256'), 'hex')
               ),
               documento_mascarado = coalesce(nullif(documento_mascarado, ''), nullif(documento, ''), 'NÃO INFORMADO'),
               razao_social = coalesce(nullif(razao_social, ''), nullif(nome, ''), 'Fornecedor legado'),
               nome_fantasia = coalesce(nullif(nome_fantasia, ''), nullif(nome, '')),
               prazo_medio = greatest(coalesce(prazo_medio, 0), 0),
               score = least(greatest(coalesce(score, 0), 0), 100),
               status = case
                   when status in ('RASCUNHO','EM_ANALISE','ATIVO','SUSPENSO','BLOQUEADO','INATIVO') then status
                   when coalesce(ativo, true) then 'ATIVO'
                   else 'INATIVO'
               end,
               created_by = coalesce(nullif(created_by, ''), 'migration'),
               updated_by = coalesce(nullif(updated_by, ''), 'migration'),
               correlation_id = coalesce(nullif(correlation_id, ''), 'legacy-' || id::text),
               version = greatest(coalesce(version, 1), 1),
               is_deleted = coalesce(is_deleted, false);

        alter table sigov.compras_fornecedor
            alter column codigo set not null,
            alter column documento_hash set not null,
            alter column documento_mascarado set not null,
            alter column razao_social set not null,
            alter column correlation_id set not null;

        create unique index if not exists ux_compras_fornecedor_codigo_compat
            on sigov.compras_fornecedor(tenant_id, codigo);
        create unique index if not exists ux_compras_fornecedor_documento_compat
            on sigov.compras_fornecedor(tenant_id, documento_hash);
    end if;

    -- O pedido básico permanece com os identificadores originais, mas recebe o
    -- contrato mínimo esperado pelos serviços e repositórios do módulo completo.
    if to_regclass('sigov.compras_pedido') is not null then
        alter table sigov.compras_pedido
            add column if not exists requisicao_id uuid,
            add column if not exists cotacao_id uuid,
            add column if not exists total numeric(18,2) not null default 0,
            add column if not exists previsao date,
            add column if not exists created_at timestamptz not null default now(),
            add column if not exists created_by varchar(100) not null default 'migration',
            add column if not exists updated_at timestamptz not null default now(),
            add column if not exists updated_by varchar(100) not null default 'migration',
            add column if not exists correlation_id varchar(100),
            add column if not exists version bigint not null default 1,
            add column if not exists is_deleted boolean not null default false;

        update sigov.compras_pedido
           set total = case
                   when coalesce(total, 0) > 0 then total
                   else greatest(coalesce(valor_total, 0), 0)
               end,
               created_by = coalesce(nullif(created_by, ''), 'migration'),
               updated_by = coalesce(nullif(updated_by, ''), 'migration'),
               correlation_id = coalesce(nullif(correlation_id, ''), 'legacy-' || id::text),
               version = greatest(coalesce(version, 1), 1),
               is_deleted = coalesce(is_deleted, false);

        alter table sigov.compras_pedido
            alter column correlation_id set not null;
    end if;
end $$;
