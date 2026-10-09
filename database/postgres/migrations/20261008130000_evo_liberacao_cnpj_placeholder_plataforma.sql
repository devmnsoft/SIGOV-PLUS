-- ============================================================
-- RC-EVO-B: liberacao do CNPJ placeholder global para o seed dev 999.
-- Contexto: a migration publicada 20261006110000 (evo_a03) cria, em
-- banco novo, a entidade 'Plataforma MNSOFT' (tenant plataforma-mnsoft)
-- com cnpj '00000000000000'. A unicidade de sigov.entidade.cnpj e
-- global (ux_entidade_cnpj), e o seed de desenvolvimento
-- 999_super_admin_access_guard.sql insere a 'Entidade Principal' do
-- tenant 'sigov-local' com o mesmo cnpj, sempre por caminho
-- "insert ... where not exists (tenant, cnpj)". Em instalacao limpa do
-- script_completo_dev.sql isso viola ux_entidade_cnpj (regras 9/13).
-- Correcao por migration nova (regra 8: nao alterar publicada): move o
-- placeholder da plataforma para '00000000000090' (ficticio, regra 10),
-- preservando qualquer outro ocupante legitimo do ...90 e sem tocar em
-- demais tenants (multi-esfera). Idempotente.
-- ============================================================

do $evo_b_cnpj$
declare
    v_tenant_plataforma bigint;
begin
    select t.id into v_tenant_plataforma
      from sigov.tenant t
     where t.slug = 'plataforma-mnsoft';

    if v_tenant_plataforma is null then
        return;
    end if;

    update sigov.entidade
       set cnpj = '00000000000090',
           updated_at = now()
     where tenant_id = v_tenant_plataforma
       and cnpj = '00000000000000'
       and not exists (
           select 1
             from sigov.entidade outro
            where outro.cnpj = '00000000000090'
       );
end
$evo_b_cnpj$;
