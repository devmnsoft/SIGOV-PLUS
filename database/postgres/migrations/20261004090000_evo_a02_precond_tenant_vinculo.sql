-- ============================================================
-- RC-EVO (Bloco A) | 20261004090000
-- Precondicao de instalacao limpa: fn_preencher_tenant_vinculo e
-- triggers de derivacao de tenant_id nos vinculos de acesso
-- (usuario_entidade, usuario_exercicio, usuario_grupo,
--  grupo_perfil, perfil_permissao).
--
-- Raiz do defeito (baseline 2026-10-06, banco descartavel):
--   Na instalacao limpa a execucao parava em 20261005090000
--   (RC-SAAS-AUT aut_01) com "null value in column tenant_id of
--   relation perfil_permissao". Os grants da aut_01 inserem em
--   perfil_permissao sem tenant_id, confiando no trigger
--   trg_perfil_permissao_tenant para derivar o tenant do
--   perfil_acesso. O DDL dessa funcao e desses triggers existia
--   apenas no bootstrap 850_post_migration_compatibility.sql, que
--   roda DEPOIS de todas as migrations; nenhuma migration de
--   arquivo os criava. Em bancos legados/upgrade o bootstrap ja
--   havia criado os objetos, por isso a falha so aparecia em
--   instalacao limpa.
--
-- Correcao (regra 8: migration nova; nenhuma publicada e alterada):
--   Materializa a precondicao nesta migration, posicionada entre
--   20261003140000 e 20261005090000. O DDL e identico ao estagio
--   850 (create or replace + drop/create trigger); o bootstrap
--   reexecuta depois como no-op idempotente. Grants posteriores de
--   escopo plataforma (20261006120000) e evo_a01 (20261006233000)
--   ja desabilitam os triggers user ao redor dos respectivos
--   upserts, sem conflito. As cinco tabelas ja existem desde 004/013.
-- Idempotente; sem credencial literal; falha explicita via
-- postConditionSql (regra 13).
-- ============================================================

create or replace function sigov.fn_preencher_tenant_vinculo()
returns trigger
language plpgsql
as $$
begin
    if new.tenant_id is not null then
        return new;
    end if;

    case tg_table_name
        when 'usuario_entidade' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select e.tenant_id into new.tenant_id
                  from sigov.entidade e
                 where e.id = new.entidade_id;
            end if;

        when 'usuario_exercicio' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select x.tenant_id into new.tenant_id
                  from sigov.exercicio x
                 where x.id = new.exercicio_id;
            end if;

        when 'usuario_grupo' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select g.tenant_id into new.tenant_id
                  from sigov.grupo_acesso g
                 where g.id = new.grupo_acesso_id;
            end if;

        when 'grupo_perfil' then
            select g.tenant_id into new.tenant_id
              from sigov.grupo_acesso g
             where g.id = new.grupo_acesso_id;
            if new.tenant_id is null then
                select p.tenant_id into new.tenant_id
                  from sigov.perfil_acesso p
                 where p.id = new.perfil_acesso_id;
            end if;

        when 'perfil_permissao' then
            select p.tenant_id into new.tenant_id
              from sigov.perfil_acesso p
             where p.id = new.perfil_acesso_id;
    end case;

    if new.tenant_id is null then
        raise exception 'Não foi possível determinar tenant_id para %.', tg_table_name
            using errcode = '23502';
    end if;

    return new;
end $$;

drop trigger if exists trg_usuario_entidade_tenant on sigov.usuario_entidade;
create trigger trg_usuario_entidade_tenant
before insert or update on sigov.usuario_entidade
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_usuario_exercicio_tenant on sigov.usuario_exercicio;
create trigger trg_usuario_exercicio_tenant
before insert or update on sigov.usuario_exercicio
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_usuario_grupo_tenant on sigov.usuario_grupo;
create trigger trg_usuario_grupo_tenant
before insert or update on sigov.usuario_grupo
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_grupo_perfil_tenant on sigov.grupo_perfil;
create trigger trg_grupo_perfil_tenant
before insert or update on sigov.grupo_perfil
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_perfil_permissao_tenant on sigov.perfil_permissao;
create trigger trg_perfil_permissao_tenant
before insert or update on sigov.perfil_permissao
for each row execute function sigov.fn_preencher_tenant_vinculo();
