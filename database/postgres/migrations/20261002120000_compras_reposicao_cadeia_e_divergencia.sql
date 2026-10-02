-- Cadeia de reposicao, justificativa e rastreabilidade de divergencias sucessivas.
-- Multi-esfera: compativel com municipios, estados e Uniao. Idempotente.

alter table sigov.compras_empresarial_recebimento_divergencia
    add column if not exists divergencia_origem_id bigint references sigov.compras_empresarial_recebimento_divergencia(id),
    add column if not exists reposicao_justificativa text;

create index if not exists ix_ce_recb_div_divergencia_origem
    on sigov.compras_empresarial_recebimento_divergencia(tenant_id, divergencia_origem_id);

alter table sigov.compras_empresarial_divergencia_idempotencia
    alter column operacao type varchar(32);

do $$
begin
  if exists (select 1 from pg_constraint where conname = 'compras_empresarial_divergencia_idempotencia_operacao_check') then
    alter table sigov.compras_empresarial_divergencia_idempotencia drop constraint compras_empresarial_divergencia_idempotencia_operacao_check;
  end if;
  if not exists (select 1 from pg_constraint where conname = 'ck_comp_div_idem_operacao') then
    alter table sigov.compras_empresarial_divergencia_idempotencia add constraint ck_comp_div_idem_operacao check(operacao in('ATRIBUIR','ANDAMENTO','ENCERRAR','AUTORIZAR_REPOSICAO'));
  end if;
end $$;
