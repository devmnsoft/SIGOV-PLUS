-- SIGOV+ RC38E
-- Pré-voo idempotente para reparar estruturas legadas antes das migrations críticas.
-- Não cria dados operacionais; garante somente as colunas e valores mínimos exigidos
-- por migrations históricas que usam CREATE TABLE IF NOT EXISTS seguido de índices
-- ou constraints sobre estruturas que já podem existir em formato antigo.

create schema if not exists sigov;
create schema if not exists plantaopro;

-- As tabelas genéricas do schema plantaopro podem existir em formato legado.
-- A migration 20260608120000 cria índices em todas estas colunas; portanto elas
-- precisam existir antes de o script chegar àquele bloco.
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

-- A migration financeira pós-RC cria constraints sobre os quatro campos abaixo.
-- Em instalações legadas a tabela pode existir sem esses campos ou com registros que
-- não atendem ao novo contrato. A normalização preserva os valores válidos e ajusta
-- somente registros incompatíveis, antes de a constraint ser criada.
do $$
declare
    v_table text;
begin
    foreach v_table in array array['financeiro_conta_receber', 'financeiro_conta_pagar']
    loop
        if to_regclass(format('sigov.%I', v_table)) is null then
            continue;
        end if;

        execute format(
            'alter table sigov.%I
                add column if not exists valor_original numeric(14,2) not null default 0,
                add column if not exists valor_desconto numeric(14,2) not null default 0,
                add column if not exists valor_acrescimo numeric(14,2) not null default 0,
                add column if not exists valor_aberto numeric(14,2) not null default 0',
            v_table
        );

        -- Copiar total de colunas legadas conhecidas somente quando valor_original
        -- ainda não possui um valor positivo.
        if exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = v_table and column_name = 'valor_total'
        ) then
            execute format(
                'update sigov.%I set valor_original = greatest(coalesce(valor_total, 0), 0)
                  where coalesce(valor_original, 0) <= 0 and coalesce(valor_total, 0) > 0',
                v_table
            );
        end if;

        if exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = v_table and column_name = 'valor'
        ) then
            execute format(
                'update sigov.%I set valor_original = greatest(coalesce(valor, 0), 0)
                  where coalesce(valor_original, 0) <= 0 and coalesce(valor, 0) > 0',
                v_table
            );
        end if;

        -- Preservar estruturas antigas que separavam juros e multa.
        if exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = v_table and column_name = 'valor_juros'
        ) and exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = v_table and column_name = 'valor_multa'
        ) then
            execute format(
                'update sigov.%I
                    set valor_acrescimo = greatest(coalesce(valor_acrescimo, 0), 0)
                                         + greatest(coalesce(valor_juros, 0), 0)
                                         + greatest(coalesce(valor_multa, 0), 0)
                  where coalesce(valor_acrescimo, 0) = 0
                    and (coalesce(valor_juros, 0) <> 0 or coalesce(valor_multa, 0) <> 0)',
                v_table
            );
        end if;

        -- Garantir exatamente o domínio exigido por ck_fin_cr_valores/ck_fin_cp_valores.
        execute format(
            'update sigov.%I
                set valor_desconto = greatest(coalesce(valor_desconto, 0), 0),
                    valor_acrescimo = greatest(coalesce(valor_acrescimo, 0), 0),
                    valor_original = greatest(
                        coalesce(valor_original, 0),
                        greatest(coalesce(valor_aberto, 0), 0) - greatest(coalesce(valor_acrescimo, 0), 0),
                        0.01
                    )',
            v_table
        );

        execute format(
            'update sigov.%I
                set valor_aberto = least(
                    greatest(coalesce(valor_aberto, 0), 0),
                    valor_original + valor_acrescimo
                )',
            v_table
        );
    end loop;
end $$;
