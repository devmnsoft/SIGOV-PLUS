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

-- As tabelas de Ordem de Serviço foram criadas inicialmente com um contrato menor.
-- A migration pós-RC 32 usa CREATE TABLE IF NOT EXISTS e, em seguida, cria índices
-- parciais sobre colunas novas. Este bloco promove as tabelas antigas para o contrato
-- novo antes da criação dos índices, preservando os registros já existentes.
do $$
begin
    if to_regclass('sigov.os_ordem_servico') is not null then
        alter table sigov.os_ordem_servico
            add column if not exists cliente_nome varchar(250),
            add column if not exists proposta_id uuid,
            add column if not exists tecnico_id uuid,
            add column if not exists equipe_id uuid,
            add column if not exists prioridade varchar(20) not null default 'NORMAL',
            add column if not exists origem varchar(30) not null default 'MANUAL',
            add column if not exists endereco text,
            add column if not exists prazo_sla timestamptz,
            add column if not exists agendada_inicio timestamptz,
            add column if not exists agendada_fim timestamptz,
            add column if not exists inicio_real timestamptz,
            add column if not exists conclusao_em timestamptz,
            add column if not exists custo_real numeric(18,2) not null default 0,
            add column if not exists version bigint not null default 1,
            add column if not exists is_deleted boolean not null default false,
            add column if not exists created_by varchar(80) not null default 'migration',
            add column if not exists updated_by varchar(80) not null default 'migration',
            add column if not exists correlation_id varchar(120);

        update sigov.os_ordem_servico
           set cliente_nome = coalesce(nullif(cliente_nome, ''), 'Não informado'),
               descricao = coalesce(descricao, ''),
               prioridade = coalesce(nullif(prioridade, ''), 'NORMAL'),
               origem = coalesce(nullif(origem, ''), 'MANUAL'),
               version = greatest(coalesce(version, 1), 1),
               is_deleted = coalesce(is_deleted, false),
               created_by = coalesce(nullif(created_by, ''), 'migration'),
               updated_by = coalesce(nullif(updated_by, ''), 'migration');

        if exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = 'os_ordem_servico' and column_name = 'agendada_para'
        ) then
            update sigov.os_ordem_servico
               set agendada_inicio = coalesce(agendada_inicio, agendada_para);
        end if;

        if exists (
            select 1 from information_schema.columns
             where table_schema = 'sigov' and table_name = 'os_ordem_servico' and column_name = 'concluida_em'
        ) then
            update sigov.os_ordem_servico
               set conclusao_em = coalesce(conclusao_em, concluida_em);
        end if;

        alter table sigov.os_ordem_servico
            alter column cliente_nome set default 'Não informado',
            alter column cliente_nome set not null,
            alter column descricao set default '',
            alter column descricao set not null;
    end if;

    if to_regclass('sigov.os_item') is not null then
        alter table sigov.os_item
            add column if not exists unidade varchar(20) not null default 'UN',
            add column if not exists ordem integer not null default 1,
            add column if not exists executado boolean not null default false,
            add column if not exists justificativa text,
            add column if not exists version bigint not null default 1,
            add column if not exists is_deleted boolean not null default false,
            add column if not exists created_at timestamptz not null default now(),
            add column if not exists updated_at timestamptz not null default now(),
            add column if not exists created_by varchar(80) not null default 'migration',
            add column if not exists updated_by varchar(80) not null default 'migration',
            add column if not exists correlation_id varchar(120);
    end if;

    if to_regclass('sigov.os_apontamento') is not null then
        alter table sigov.os_apontamento
            add column if not exists atividade text not null default 'Atividade',
            add column if not exists intervalo_minutos integer not null default 0,
            add column if not exists idempotency_key varchar(200),
            add column if not exists version bigint not null default 1,
            add column if not exists is_deleted boolean not null default false,
            add column if not exists created_at timestamptz not null default now(),
            add column if not exists updated_at timestamptz not null default now(),
            add column if not exists created_by varchar(80) not null default 'migration',
            add column if not exists updated_by varchar(80) not null default 'migration',
            add column if not exists correlation_id varchar(120);

        update sigov.os_apontamento
           set idempotency_key = coalesce(nullif(idempotency_key, ''), 'legacy-' || id::text),
               atividade = coalesce(nullif(atividade, ''), 'Atividade'),
               intervalo_minutos = greatest(coalesce(intervalo_minutos, 0), 0),
               is_deleted = coalesce(is_deleted, false);

        alter table sigov.os_apontamento
            alter column idempotency_key set not null;

        create unique index if not exists ux_os_apontamento_idempotency_compat
            on sigov.os_apontamento(tenant_id, idempotency_key)
            where not is_deleted;
    end if;

    if to_regclass('sigov.os_status_historico') is not null then
        alter table sigov.os_status_historico
            add column if not exists observacao text,
            add column if not exists version bigint not null default 1,
            add column if not exists updated_at timestamptz not null default now(),
            add column if not exists created_by varchar(80) not null default 'migration',
            add column if not exists updated_by varchar(80) not null default 'migration';
    end if;
end $$;
