-- Compatibilidade aditiva para schemas financeiros criados por versões legadas.
-- Deve preceder a migration 20260730180000, que cria índices sobre estas colunas.
do $$
declare
    required_table text;
begin
    foreach required_table in array array[
        'financeiro_conta_receber',
        'financeiro_conta_pagar',
        'financeiro_movimento'
    ] loop
        if not exists (
            select 1
              from information_schema.tables
             where table_schema = 'sigov'
               and table_name = required_table
        ) then
            raise exception 'Tabela obrigatória sigov.% não encontrada; aplique as migrations anteriores primeiro.', required_table;
        end if;
    end loop;

    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_receber' and column_name = 'tenant_id') then
        alter table sigov.financeiro_conta_receber add column tenant_id bigint;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_receber' and column_name = 'parcela') then
        alter table sigov.financeiro_conta_receber add column parcela integer not null default 1;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_receber' and column_name = 'competencia') then
        alter table sigov.financeiro_conta_receber add column competencia date;
    end if;

    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_pagar' and column_name = 'tenant_id') then
        alter table sigov.financeiro_conta_pagar add column tenant_id bigint;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_pagar' and column_name = 'parcela') then
        alter table sigov.financeiro_conta_pagar add column parcela integer not null default 1;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_conta_pagar' and column_name = 'competencia') then
        alter table sigov.financeiro_conta_pagar add column competencia date;
    end if;

    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_movimento' and column_name = 'tenant_id') then
        alter table sigov.financeiro_movimento add column tenant_id bigint;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_movimento' and column_name = 'conta_bancaria_id') then
        alter table sigov.financeiro_movimento add column conta_bancaria_id bigint;
    end if;
    if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = 'financeiro_movimento' and column_name = 'data_movimento') then
        alter table sigov.financeiro_movimento add column data_movimento timestamptz not null default now();
    end if;
end
$$;
