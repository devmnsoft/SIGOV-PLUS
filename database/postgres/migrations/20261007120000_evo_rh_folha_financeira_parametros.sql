-- RC-EVO-RH s9 | 20261007120000 - Parametros FOLHA da relacao financeira da integracao de ponto (idempotente).
-- A relacao folha de ponto -> Financeiro (secao 9) usa parametros como regras (banco e a
-- autoridade, regra 11); o consumidor da fila so age com regras suficientes:
--   P1  FOLHA/ORCAMENTO_DESPESA_FOLHA_ID (INTEGER) - orcamento de despesa (sigov.orcamento_despesa)
--       usado no empenho gerado a partir da integracao. valor_padrao 'null' => fail-closed:
--       tenant sem mapping falha explicitamente com REGRAS_FINANCEIRAS_INSUFICIENTES
--       (sem documento ficticio, regra 13).
--   P2  FOLHA/FORNECEDOR_FOLHA_ID (INTEGER) - fornecedor (sigov.pessoa) referenciado no
--       empenho. valor_padrao 'null' => mesmo fail-closed.
--   P3  HABILITAR_INTEGRACAO_FINANCEIRA ja existe no catalogo (id 30, padrao false) e NUNCA e
--       semeado true aqui: a ativacao e ato do administrador via API (PUT api/parametros/FOLHA),
--       mantendo o banco como fonte de autoridade (regra 11).
--   P4  Seed FICTICIO (regras 9/10) somente para o tenant de desenvolvimento/homologacao (5):
--       orcamento=1 / fornecedor=3 (registros ficticios ja existentes no banco dev), com
--       historico auditado (origem SEED_FICTICIO_DEV). Idempotente via not exists - reexecucao
--       nao sobrescreve valor que o administrador ja tenha ajustado.
-- Neutro multi-esfera (regras 21-24): o mapping e por tenant (parametro_modulo_valor), sem
-- hardcode municipal; sem taxa, percentual ou regra legal inventada; sem credencial literal;
-- autoverificacao com falha explicita (regra 13).

insert into sigov.parametro_modulo (modulo, codigo, nome, descricao, tipo, valor_padrao, sensivel, ordem)
values ('FOLHA','ORCAMENTO_DESPESA_FOLHA_ID','Orcamento de despesa padrao da folha de ponto',
        'Identificador (numero inteiro) do item sigov.orcamento_despesa usado pelo consumidor da fila ao emitir o empenho da integracao de ponto na folha. Nulo ou indefinido gera a falha explicita REGRAS_FINANCEIRAS_INSUFICIENTES.',
        'INTEGER','null'::jsonb,false,101)
on conflict (modulo,codigo) where is_deleted=false
do update set nome = excluded.nome, descricao = excluded.descricao, tipo = excluded.tipo, ativo = true, is_deleted = false;

insert into sigov.parametro_modulo (modulo, codigo, nome, descricao, tipo, valor_padrao, sensivel, ordem)
values ('FOLHA','FORNECEDOR_FOLHA_ID','Fornecedor padrao da folha de ponto',
        'Identificador (numero inteiro) da sigov.pessoa (fornecedor) referenciada no empenho da integracao de ponto na folha. Nulo ou indefinido gera a falha explicita REGRAS_FINANCEIRAS_INSUFICIENTES.',
        'INTEGER','null'::jsonb,false,102)
on conflict (modulo,codigo) where is_deleted=false
do update set nome = excluded.nome, descricao = excluded.descricao, tipo = excluded.tipo, ativo = true, is_deleted = false;

do $folha_financeira$
declare
    v_orcamento bigint;
    v_fornecedor bigint;
    v_tenant_id bigint;
begin
    select id into v_orcamento
      from sigov.parametro_modulo
     where modulo = 'FOLHA' and codigo = 'ORCAMENTO_DESPESA_FOLHA_ID' and ativo and not is_deleted;
    select id into v_fornecedor
      from sigov.parametro_modulo
     where modulo = 'FOLHA' and codigo = 'FORNECEDOR_FOLHA_ID' and ativo and not is_deleted;

    if v_orcamento is null or v_fornecedor is null then
        raise exception 'RC-EVO-RH 20261007120000: parametro FOLHA/ORCAMENTO_DESPESA_FOLHA_ID ou FOLHA/FORNECEDOR_FOLHA_ID ausente do catalogo apos upsert';
    end if;

    -- Mapping FICTICIO somente para o tenant de desenvolvimento/homologacao (regras 9/10):
    -- orcamento_despesa id 1 e pessoa id 3 (Fornecedor Ficticio de Homologacao) ja existem
    -- no banco dev. Reexecucao nao sobrescreve (not exists), preservando ajuste do administrador.
    for v_tenant_id in select id from sigov.tenant where id = 5 and ativo and not is_deleted loop
        if not exists (select 1 from sigov.parametro_modulo_valor
                       where tenant_id = v_tenant_id and parametro_id = v_orcamento and not is_deleted) then
            insert into sigov.parametro_modulo_valor (tenant_id, parametro_id, valor, created_by, updated_by, correlation_id)
            values (v_tenant_id, v_orcamento, '1'::jsonb, null, null, 'evo-rh-folha-financeira-seed');
            insert into sigov.parametro_modulo_historico (tenant_id, parametro_id, valor_anterior, valor_novo, usuario_id, correlation_id, auditoria)
            values (v_tenant_id, v_orcamento, null, '1'::jsonb, null, 'evo-rh-folha-financeira-seed', '{"origem":"SEED_FICTICIO_DEV"}'::jsonb);
        end if;

        if not exists (select 1 from sigov.parametro_modulo_valor
                       where tenant_id = v_tenant_id and parametro_id = v_fornecedor and not is_deleted) then
            insert into sigov.parametro_modulo_valor (tenant_id, parametro_id, valor, created_by, updated_by, correlation_id)
            values (v_tenant_id, v_fornecedor, '3'::jsonb, null, null, 'evo-rh-folha-financeira-seed');
            insert into sigov.parametro_modulo_historico (tenant_id, parametro_id, valor_anterior, valor_novo, usuario_id, correlation_id, auditoria)
            values (v_tenant_id, v_fornecedor, null, '3'::jsonb, null, 'evo-rh-folha-financeira-seed', '{"origem":"SEED_FICTICIO_DEV"}'::jsonb);
        end if;
    end loop;
end $folha_financeira$;

do $check$
begin
    if not exists (select 1 from sigov.parametro_modulo
                   where modulo = 'FOLHA' and codigo = 'ORCAMENTO_DESPESA_FOLHA_ID' and tipo = 'INTEGER' and ativo and not is_deleted)
       or not exists (select 1 from sigov.parametro_modulo
                      where modulo = 'FOLHA' and codigo = 'FORNECEDOR_FOLHA_ID' and tipo = 'INTEGER' and ativo and not is_deleted)
    then
        raise exception 'RC-EVO-RH 20261007120000: parametros FOLHA da relacao financeira ausentes apos seed';
    end if;

    raise notice 'RC-EVO-RH 20261007120000 concluida: ORCAMENTO_DESPESA_FOLHA_ID e FORNECEDOR_FOLHA_ID no catalogo FOLHA c/ seed ficticio dev (tenant 5: orcamento=1, fornecedor=3). HABILITAR_INTEGRACAO_FINANCEIRA permanece desligada por padrao.';
end $check$;
