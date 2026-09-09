-- Compatibilidade forward-only dos contratos financeiros anteriores à RC50.86.
-- O contexto multi-esfera é derivado exclusivamente da entidade persistida.
-- Dados legados sem esfera configurada interrompem explicitamente a aplicação.
alter table if exists sigov.entidade
    add column if not exists esfera_governo varchar(12);

do $$
declare
    tabela text;
    possui_sem_esfera boolean;
    tabelas text[] := array[
        'financeiro_acao', 'financeiro_conciliacao_bancaria', 'financeiro_conciliacao_item',
        'financeiro_conta_bancaria', 'financeiro_dotacao', 'financeiro_empenho',
        'financeiro_empenho_item', 'financeiro_empenho_movimento', 'financeiro_fonte_recurso',
        'financeiro_liquidacao', 'financeiro_natureza_despesa', 'financeiro_pagamento',
        'financeiro_programa', 'financeiro_receita_arrecadada', 'financeiro_receita_prevista',
        'financeiro_transferencia', 'financeiro_unidade_orcamentaria'
    ];
begin
    foreach tabela in array tabelas loop
        if to_regclass('sigov.' || tabela) is null then
            continue;
        end if;

        execute format('alter table sigov.%I add column if not exists tenant_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists entidade_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists exercicio_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists esfera_governo varchar(10)', tabela);
        execute format('alter table sigov.%I add column if not exists orgao_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_gestora_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_executora_id bigint', tabela);
        execute format(
            'update sigov.%1$I registro set esfera_governo=entidade.esfera_governo from sigov.entidade entidade where entidade.id=registro.entidade_id and registro.esfera_governo is null',
            tabela);

        execute format('select exists(select 1 from sigov.%I where tenant_id is null or entidade_id is null or exercicio_id is null or esfera_governo is null)', tabela)
            into possui_sem_esfera;
        if possui_sem_esfera then
            raise exception 'Tabela sigov.% contém dados sem contexto tenant/entidade/exercício/esfera completo', tabela;
        end if;

        execute format('alter table sigov.%I alter column tenant_id set not null, alter column entidade_id set not null, alter column exercicio_id set not null, alter column esfera_governo set not null', tabela);
        if not exists(select 1 from pg_constraint
                      where conrelid=to_regclass('sigov.' || tabela)
                        and conname=('ck_' || tabela || '_esfera')) then
            execute format(
                'alter table sigov.%1$I add constraint %2$I check(esfera_governo in (''municipal'',''estadual'',''federal''))',
                tabela, 'ck_' || tabela || '_esfera');
        end if;
    end loop;
end $$;
