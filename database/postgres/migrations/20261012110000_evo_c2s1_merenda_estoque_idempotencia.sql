-- ============================================================================
-- RC-EVO Ciclo 2 Sprint 1 (merenda integridade transacional): coluna de
-- idempotencia + backstops de unicidade do estoque/estorno.
--   (a) educacao_merenda_movimento.chave_idempotencia + indice unico parcial
--       por tenant — reenvio do mesmo comando tem efeito unico (BR-1.6/1.7).
--   (b) um estorno por movimento-base x produto (tipo='ESTORNO') — compensacao
--       unica (BR-1.7/1.8).
--   (c) dedupe idempotente de linhas legadas de estoque (mantem min(id), soma
--       os saldos) + indice unico parcial tenant x produto — criaçao
--       concorrente nunca duplica a linha (BR-1.3).
-- Idempotente; multi-esfera (regra 24): chaves por tenant, validas para qualquer
-- esfera/orgao. Pos-condicao explicita (regra 13).
-- ============================================================================

alter table sigov.educacao_merenda_movimento add column if not exists chave_idempotencia text;

create unique index if not exists ux_merenda_movimento_tenant_chave
    on sigov.educacao_merenda_movimento (tenant_id, chave_idempotencia)
    where chave_idempotencia is not null
      and is_deleted = false;

create unique index if not exists ux_merenda_movimento_tenant_estorno_ref
    on sigov.educacao_merenda_movimento (tenant_id, referencia_id, produto_id)
    where tipo = 'ESTORNO'
      and referencia_id is not null
      and produto_id is not null
      and is_deleted = false;

with rankes as (
    select e.id,
           row_number() over (partition by e.tenant_id, e.produto_id order by e.id) as rn,
           sum(coalesce(e.saldo, 0)) over (partition by e.tenant_id, e.produto_id) as soma
    from sigov.educacao_merenda_estoque e
    where e.is_deleted = false
)
update sigov.educacao_merenda_estoque set saldo = d.soma, updated_at = now()
from rankes d
where sigov.educacao_merenda_estoque.id = d.id
  and d.rn = 1
  and coalesce(d.soma, 0) <> coalesce(sigov.educacao_merenda_estoque.saldo, 0);

with rankes as (
    select e.id,
           row_number() over (partition by e.tenant_id, e.produto_id order by e.id) as rn
    from sigov.educacao_merenda_estoque e
    where e.is_deleted = false
)
update sigov.educacao_merenda_estoque set is_deleted = true, updated_at = now()
from rankes d
where sigov.educacao_merenda_estoque.id = d.id
  and d.rn > 1;

create unique index if not exists ux_merenda_estoque_tenant_produto
    on sigov.educacao_merenda_estoque (tenant_id, produto_id)
    where produto_id is not null
      and is_deleted = false;

-- Pos-condicao explicita (regra 13): sem os artefatos, falha nomeada.
do $post$
begin
    if not exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='educacao_merenda_movimento' and column_name='chave_idempotencia') then
        raise exception 'C2S1-POSTCOND: coluna chave_idempotencia ausente em sigov.educacao_merenda_movimento';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_merenda_movimento_tenant_chave') then
        raise exception 'C2S1-POSTCOND: indice ux_merenda_movimento_tenant_chave ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_merenda_movimento_tenant_estorno_ref') then
        raise exception 'C2S1-POSTCOND: indice ux_merenda_movimento_tenant_estorno_ref ausente';
    end if;
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_merenda_estoque_tenant_produto') then
        raise exception 'C2S1-POSTCOND: indice ux_merenda_estoque_tenant_produto ausente';
    end if;
end $post$;
