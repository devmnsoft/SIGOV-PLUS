-- RC-SAAS-AUT Etapa B - Comercial família B: quantidade dos addons de usuários/armazenamento
-- e política de downgrade (parametrizada por esfera, banco como autoridade).
-- Idempotente: colunas add-if-not-exists, constraint drop/add, seeds com where-not-exists/update-if-null.

alter table sigov.saas_addon add column if not exists valor_quantidade int null;
alter table sigov.saas_addon add column if not exists unidade_quantidade varchar(40) null;

alter table sigov.saas_addon drop constraint if exists ck_saas_addon_valor_quantidade;
alter table sigov.saas_addon add constraint ck_saas_addon_valor_quantidade check (valor_quantidade is null or valor_quantidade > 0);

update sigov.saas_addon set valor_quantidade = 10, unidade_quantidade = 'usuarios' where codigo = 'USUARIOS_EXTRAS' and valor_quantidade is null;
update sigov.saas_addon set valor_quantidade = 5120, unidade_quantidade = 'MB' where codigo = 'ARMAZENAMENTO_EXTRA' and valor_quantidade is null;

insert into sigov.parametro_modulo (modulo, codigo, nome, descricao, tipo, valor_padrao, sensivel, ordem)
select 'SAAS', 'DOWNGRADE_BLOQUEIO_NOVAS_ALOCACOES', 'Downgrade: suspensão de módulos excedentes', 'Política de downgrade comercial da família B. true (padrão): módulos fora do novo plano passam para SUSPENSO no contrato do cliente (família C), preservando dados e bloqueando novas alocações. false: mantém os módulos excedentes operando durante a transição.', 'BOOLEAN', 'true'::jsonb, false, 10
where not exists (select 1 from sigov.parametro_modulo p where p.modulo = 'SAAS' and p.codigo = 'DOWNGRADE_BLOQUEIO_NOVAS_ALOCACOES');
