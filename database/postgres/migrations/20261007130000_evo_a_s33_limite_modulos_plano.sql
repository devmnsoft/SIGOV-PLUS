-- RC-EVO-A S3.3 | 20261007130000 - Limite comercial de modulos do plano (idempotente).
-- saas_plano.limite_modulos e a cota de contratos de modulo vigentes que o tenant pode deter:
-- null = ilimitado (mesma semantica de limite_usuarios), inteiro >= 0 = teto. Nao se semeia valor
-- (regra 11): cada plano recebe sua cota por ato administrativo dentro do banco. O enforcement e
-- transacional em codigo (SaasLimitValidator.ValidateModuleLimitTxAsync +
-- SaasTenantAdministrationService.MutateAsync): trava as assinaturas ativas (lock single-table,
-- mesma licao do S3.2) e conta contratos vigentes na fonte contratual
-- sigov.tenant_modulo_contratado com a mesma janela de status/vigencia do dependency check; a
-- contagem sob READ COMMITTED usa snapshot fresco e serializa a disputa pela ultima vaga.
-- Sem bonus, sem ADDON inventado; neutro multi-esfera (regras 21-24); sem credencial literal;
-- autoverificacao com falha explicita (regra 13).

alter table sigov.saas_plano add column if not exists limite_modulos int;

alter table sigov.saas_plano drop constraint if exists ck_saas_plano_limites;
alter table sigov.saas_plano add constraint ck_saas_plano_limites
    check ((limite_usuarios is null or limite_usuarios >= 0) and (limite_entidades is null or limite_entidades >= 0) and (limite_armazenamento_mb is null or limite_armazenamento_mb >= 0) and (limite_modulos is null or limite_modulos >= 0));

do $s33$
begin
    if not exists (select 1 from pg_attribute a join pg_class c on c.oid=a.attrelid join pg_namespace n on n.oid=c.relnamespace where n.nspname='sigov' and c.relname='saas_plano' and a.attname='limite_modulos' and not a.attisdropped) then
        raise exception 'RC-EVO-A S3.3 20261007130000: coluna limite_modulos ausente em sigov.saas_plano apos o alter';
    end if;

    if not exists (select 1 from pg_constraint c join pg_class t on t.oid=c.conrelid where c.conname='ck_saas_plano_limites' and t.relname='saas_plano' and t.relnamespace='sigov'::regnamespace and c.contype='c' and pg_get_constraintdef(c.oid) like '%limite_modulos%') then
        raise exception 'RC-EVO-A S3.3 20261007130000: check ck_saas_plano_limites sem cobertura de limite_modulos';
    end if;

    raise notice 'RC-EVO-A S3.3 20261007130000 concluida: saas_plano.limite_modulos disponivel (null = ilimitado).';
end $s33$;
