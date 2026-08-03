-- SIGOV+ RC38E
-- Pré-voo idempotente para reparar estruturas legadas antes do script_completop.sql.
-- Este arquivo não cria dados operacionais. Ele apenas garante as colunas consumidas
-- por migrations históricas que usaram CREATE TABLE IF NOT EXISTS seguido de índices.

create schema if not exists sigov;
create schema if not exists plantaopro;

-- As tabelas genéricas do schema plantaopro podem existir em formato legado.
-- A migration 20260608120000 cria índices em todas estas colunas; portanto elas
-- precisam existir antes de o script consolidado chegar àquele bloco.
do $$
declare
    r record;
begin
    for r in
        select table_name
        from information_schema.tables
        where table_schema = 'plantaopro'
          and table_type = 'BASE TABLE'
    loop
        execute format('alter table plantaopro.%I add column if not exists tenant_id bigint null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists cliente_id bigint null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists plano_id bigint null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists parceiro_id bigint null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists status varchar(60) not null default ''ATIVO''', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists codigo varchar(120) null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists nome varchar(250) null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists dominio varchar(250) null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists subdominio varchar(120) null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists api_key_hash varchar(128) null', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists dados jsonb not null default ''{}''::jsonb', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists reg_date timestamptz not null default now()', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists created_at timestamptz not null default now()', r.table_name);
        execute format('alter table plantaopro.%I add column if not exists updated_at timestamptz null', r.table_name);
    end loop;
end $$;

-- A migration financeira pós-RC adiciona uma constraint usando estas quatro colunas.
-- Em bancos criados por versões anteriores a tabela pode existir sem uma ou mais delas.
do $$
begin
    if to_regclass('sigov.financeiro_conta_receber') is not null then
        alter table sigov.financeiro_conta_receber
            add column if not exists valor_original numeric(14,2) not null default 0,
            add column if not exists valor_desconto numeric(14,2) not null default 0,
            add column if not exists valor_acrescimo numeric(14,2) not null default 0,
            add column if not exists valor_aberto numeric(14,2) not null default 0;

        -- Preserva estruturas antigas que separavam juros e multa.
        if exists (
            select 1 from information_schema.columns
            where table_schema = 'sigov' and table_name = 'financeiro_conta_receber' and column_name = 'valor_juros'
        ) and exists (
            select 1 from information_schema.columns
            where table_schema = 'sigov' and table_name = 'financeiro_conta_receber' and column_name = 'valor_multa'
        ) then
            execute 'update sigov.financeiro_conta_receber
                     set valor_acrescimo = coalesce(valor_acrescimo, 0) + coalesce(valor_juros, 0) + coalesce(valor_multa, 0)
                     where coalesce(valor_acrescimo, 0) = 0
                       and (coalesce(valor_juros, 0) <> 0 or coalesce(valor_multa, 0) <> 0)';
        end if;

        update sigov.financeiro_conta_receber
        set valor_original = greatest(coalesce(valor_original, 0), 0),
            valor_desconto = greatest(coalesce(valor_desconto, 0), 0),
            valor_acrescimo = greatest(coalesce(valor_acrescimo, 0), 0),
            valor_aberto = greatest(coalesce(valor_aberto, 0), 0);
    end if;
end $$;
