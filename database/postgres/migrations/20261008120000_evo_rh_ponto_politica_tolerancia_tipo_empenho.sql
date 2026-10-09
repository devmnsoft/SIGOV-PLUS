-- RC-EVO-B 3/5 | 20261008120000 - Parametros PONTO/FOLHA do motor v2 e do documento financeiro (idempotente).
-- O banco e a autoridade das regras (regra 11); nada aqui inventa percentual ou criterio legal:
--   P1  PONTO/POLITICA_TOLERANCIA (JSON) - politica de tolerancia de atraso na apuracao do ponto.
--       Vocabulario aceito pelo motor (PontoApuracaoEngine.InterpretarPoliticaTolerancia):
--         'SOBRE_EXCEDENTE' - atraso dentro da tolerancia so desconta o excedente apurado;
--         'ATRASO_COMPLETO' - atraso fora da tolerancia desconta o atraso inteiro.
--       valor_padrao 'null' e FAIL-CLOSED (regra 13): tenant sem politica aprovada apura com o
--       comportamento historico SOBRE_EXCEDENTE e recebe a pendencia nomeada
--       PARAMETRO_POLITICA_TOLERANCIA_AUSENTE no resultado - nunca sucesso simulado.
--   P2  FOLHA/TIPO_EMPENHO_INTEGRACAO_PONTO (JSON) - tipo do empenho emitido pelo consumidor da
--       fila financeira (FolhaPontoFinanceiraOutboxHandler). Padrao '"ORDINARIO"' apenas por ser o
--       vocabulario ja usado pela UI Financeiro; nao e invento comercial, e o tipo neutro padrao.
--   P3  Seed FICTICIO (regras 9/10) somente para o tenant de desenvolvimento/homologacao (5):
--       POLITICA_TOLERANCIA = 'SOBRE_EXCEDENTE' (comportamento historico, escolhido por ser o
--       menos punitivo e servir de linha de base dos testes de aceite), com historia auditada
--       (origem SEED_FICTICIO_DEV). Idempotente via not exists - reexecucao nao sobrescreve
--       escolha que o administrador ja tenha registrado.
-- Neutro multi-esfera (regras 21-24): parametros por tenant (parametro_modulo_valor), sem hardcode
-- municipal e sem taxa/percentual inventado; sem credencial literal; autoverificacao com falha
-- explicita (regra 13).

insert into sigov.parametro_modulo (modulo, codigo, nome, descricao, tipo, valor_padrao, sensivel, ordem)
values ('PONTO','POLITICA_TOLERANCIA','Politica de tolerancia de atraso na apuracao de ponto',
        'Define como a tolerancia de atraso incide na apuracao do ponto: ''SOBRE_EXCEDENTE'' desconta somente o excedente apurado; ''ATRASO_COMPLETO'' desconta o atraso inteiro. Nulo ou ilegitimo apura com o comportamento historico SOBRE_EXCEDENTE e grava a pendencia explicita PARAMETRO_POLITICA_TOLERANCIA_AUSENTE.',
        'JSON','null'::jsonb,false,100)
on conflict (modulo,codigo) where is_deleted=false
do update set nome = excluded.nome, descricao = excluded.descricao, tipo = excluded.tipo, ativo = true, is_deleted = false;

insert into sigov.parametro_modulo (modulo, codigo, nome, descricao, tipo, valor_padrao, sensivel, ordem)
values ('FOLHA','TIPO_EMPENHO_INTEGRACAO_PONTO','Tipo de empenho da integracao financeira de ponto',
        'Tipo do empenho emitido pelo consumidor da fila ao materializar a integracao de ponto no Financeiro. Aceita o vocabulario da UI Financeiro (ORDINARIO, GLOBAL, PATRIMONIAL, ESTIMATIVO). Ausente ou ilegivel usa ORDINARIO.',
        'JSON','"ORDINARIO"'::jsonb,false,103)
on conflict (modulo,codigo) where is_deleted=false
do update set nome = excluded.nome, descricao = excluded.descricao, tipo = excluded.tipo, ativo = true, is_deleted = false;

do $ponto_tolerancia$
declare
    v_politica bigint;
    v_tenant_id bigint;
begin
    select id into v_politica
      from sigov.parametro_modulo
     where modulo = 'PONTO' and codigo = 'POLITICA_TOLERANCIA' and ativo and not is_deleted;

    if v_politica is null then
        raise exception 'RC-EVO-B 20261008120000: parametro PONTO/POLITICA_TOLERANCIA ausente do catalogo apos upsert';
    end if;

    -- Valor FICTICIO somente para o tenant de desenvolvimento/homologacao (regras 9/10).
    -- Reexecucao nao sobrescreve (not exists), preservando a escolha do administrador.
    for v_tenant_id in select id from sigov.tenant where id = 5 and ativo and not is_deleted loop
        if not exists (select 1 from sigov.parametro_modulo_valor
                       where tenant_id = v_tenant_id and parametro_id = v_politica and not is_deleted) then
            insert into sigov.parametro_modulo_valor (tenant_id, parametro_id, valor, created_by, updated_by, correlation_id)
            values (v_tenant_id, v_politica, '"SOBRE_EXCEDENTE"'::jsonb, null, null, 'evo-rh-ponto-tolerancia-seed');
            insert into sigov.parametro_modulo_historico (tenant_id, parametro_id, valor_anterior, valor_novo, usuario_id, correlation_id, auditoria)
            values (v_tenant_id, v_politica, null, '"SOBRE_EXCEDENTE"'::jsonb, null, 'evo-rh-ponto-tolerancia-seed', '{"origem":"SEED_FICTICIO_DEV"}'::jsonb);
        end if;
    end loop;
end $ponto_tolerancia$;

do $check$
begin
    if not exists (select 1 from sigov.parametro_modulo
                   where modulo = 'PONTO' and codigo = 'POLITICA_TOLERANCIA' and tipo = 'JSON'
                     and valor_padrao = 'null'::jsonb and ativo and not is_deleted)
       or not exists (select 1 from sigov.parametro_modulo
                      where modulo = 'FOLHA' and codigo = 'TIPO_EMPENHO_INTEGRACAO_PONTO' and tipo = 'JSON'
                        and valor_padrao = '"ORDINARIO"'::jsonb and ativo and not is_deleted)
    then
        raise exception 'RC-EVO-B 20261008120000: parametros PONTO/POLITICA_TOLERANCIA ou FOLHA/TIPO_EMPENHO_INTEGRACAO_PONTO inconsistentes apos seed';
    end if;

    raise notice 'RC-EVO-B 20261008120000 concluida: PONTO/POLITICA_TOLERANCIA (fail-closed, pendencia quando ausente) e FOLHA/TIPO_EMPENHO_INTEGRACAO_PONTO (padrao ORDINARIO) no catalogo c/ seed ficticio dev (tenant 5: SOBRE_EXCEDENTE).';
end $check$;
