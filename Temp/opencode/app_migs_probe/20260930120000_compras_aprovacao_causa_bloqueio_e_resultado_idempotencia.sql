-- Jornada de aprovação: causa de bloqueio explícita por etapa e resultado original
-- persistido na idempotência. Sem política ativa a etapa nasce bloqueada (SEM_POLITICA)
-- e nunca se libera apenas atribuindo aprovador; alçada insuficiente (ALCADA_INSUFICIENTE)
-- mantém a etapa final fechada até a política cobrir o total; etapa sem aprovador
-- habilitado (SEM_APROVADOR) permanece visível e atribuível. Valores NULL preservam o legado.

alter table sigov.compras_empresarial_aprovacao
 add column if not exists causa_bloqueio varchar(40);

do $$ begin
 if not exists(select 1 from pg_constraint where conname='ck_compras_aprovacao_causa') then
  alter table sigov.compras_empresarial_aprovacao add constraint ck_compras_aprovacao_causa check(causa_bloqueio is null or causa_bloqueio in ('SEM_POLITICA','SEM_APROVADOR','ALCADA_INSUFICIENTE'));
 end if;
end $$;

alter table sigov.compras_empresarial_idempotencia
 add column if not exists resultado jsonb;
