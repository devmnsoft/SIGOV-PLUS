-- Migration: rc50_fin_jornadas_conferencia
-- Data: 2026-10-03
-- Descricao: Coluna motivo nos 5 docs, tabela idempotencia financeira,
--           3 permissoes novas + grants, CHECKs de status.
-- Idempotente: pode ser re-executado sem erro.

-- ===========================================================================
-- 1) COLUNA motivo nos documentos financeiros
-- ===========================================================================
alter table sigov.empenho add column if not exists motivo text;
alter table sigov.liquidacao add column if not exists motivo text;
alter table sigov.pagamento add column if not exists motivo text;
alter table sigov.receita_lancamento add column if not exists motivo text;
alter table sigov.receita_arrecadacao add column if not exists motivo text;

-- ===========================================================================
-- 2) TABELA de idempotencia financeira
-- ===========================================================================
create table if not exists sigov.financeiro_idempotencia (
    id            bigint generated always as identity primary key,
    tenant_id     bigint not null references sigov.tenant(id),
    escopo        varchar(60) not null,
    chave         varchar(120) not null,
    payload_hash  char(64) not null,
    documento_id  bigint,
    correlation_id uuid,
    created_at    timestamptz not null default now(),
    created_by    bigint,
    unique (tenant_id, escopo, chave)
);

create index if not exists idx_financeiro_idempotencia_tenant
    on sigov.financeiro_idempotencia (tenant_id);

-- ===========================================================================
-- 3) PERMISSOES novas
-- ===========================================================================
insert into sigov.permissao (modulo, chave, recurso, acao, descricao, ativo)
select 'financeiro', p.chave,
       split_part(p.chave,'.',1)||'.'||split_part(p.chave,'.',2),
       split_part(p.chave,'.',3),
       p.descricao, true
from (values
  ('financeiro.conferencia.visualizar','Visualizar conferência financeira'),
  ('financeiro.conferencia.ajustar','Ajustar divergência na conferência financeira'),
  ('financeiro.arrecadacao.cancelar','Cancelar arrecadação ou lançamento de receita')
) as p(chave, descricao)
on conflict (modulo, chave) do update set
    recurso = excluded.recurso,
    acao    = excluded.acao,
    descricao = excluded.descricao,
    ativo   = true,
    is_deleted = false;

-- ===========================================================================
-- 4) GRANTS para perfis financeiros (espelha pos_build_07 L54-58)
--    Conjunto: ADMINISTRADOR_GERAL, ADMIN_TENANT, FINANCEIRO_ADMIN,
--              FINANCEIRO, GERENTE_FINANCEIRO
--    Tenant de referência: slug='plataforma-global'
-- ===========================================================================
insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
select coalesce(pa.tenant_id, t.id), pa.id, p.id
from sigov.perfil_acesso pa
cross join lateral (select id from sigov.tenant where slug = 'plataforma-global' order by id limit 1) t
join sigov.permissao p on p.modulo = 'financeiro' and p.ativo = true and p.is_deleted = false
where pa.ativo = true
  and pa.is_deleted = false
  and coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in
      ('ADMINISTRADOR_GERAL','ADMIN_TENANT','FINANCEIRO_ADMIN','FINANCEIRO','GERENTE_FINANCEIRO')
  and p.chave in (
      'financeiro.conferencia.visualizar',
      'financeiro.conferencia.ajustar',
      'financeiro.arrecadacao.cancelar'
  )
  and not exists (
      select 1 from sigov.perfil_permissao pp
      where pp.tenant_id = coalesce(pa.tenant_id, t.id)
        and pp.perfil_acesso_id = pa.id
        and pp.permissao_id = p.id
  );

-- ===========================================================================
-- 5) CHECKS de status (tabelas vazias → seguro)
-- ===========================================================================
-- empenho
alter table sigov.empenho drop constraint if exists chk_empenho_status;
alter table sigov.empenho add constraint chk_empenho_status check (
    status in ('EMITIDO','LIQUIDADO_PARCIAL','LIQUIDADO_TOTAL','PAGO_PARCIAL','PAGO_TOTAL','ANULADO')
);

-- liquidacao
alter table sigov.liquidacao drop constraint if exists chk_liquidacao_status;
alter table sigov.liquidacao add constraint chk_liquidacao_status check (
    status in ('LIQUIDADA','ANULADA')
);

-- pagamento
alter table sigov.pagamento drop constraint if exists chk_pagamento_status;
alter table sigov.pagamento add constraint chk_pagamento_status check (
    status in ('EFETUADO','CANCELADO')
);

-- receita_lancamento
alter table sigov.receita_lancamento drop constraint if exists chk_receita_lancamento_status;
alter table sigov.receita_lancamento add constraint chk_receita_lancamento_status check (
    status in ('LANCADA','PARCIALMENTE_ARRECADADA','ARRECADADA','CANCELADA')
);

-- receita_arrecadacao
alter table sigov.receita_arrecadacao drop constraint if exists chk_receita_arrecadacao_status;
alter table sigov.receita_arrecadacao add constraint chk_receita_arrecadacao_status check (
    status in ('ARRECADADA','CANCELADA')
);
