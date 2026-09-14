-- Correção forward-only da compatibilidade entre a contratação canônica e a projeção legada.
-- tenant_modulo_contratado é a autoridade; nenhum estado legado concede acesso ao contrato.
create or replace function sigov.fn_tenant_modulo_compatibilizar() returns trigger
language plpgsql
security invoker
set search_path = pg_catalog, sigov
as $$
declare
    v_modulo_id bigint;
    v_habilitado boolean;
begin
    select ms.id
      into v_modulo_id
      from sigov.modulo_saas ms
     where ms.codigo = new.modulo_codigo
       and ms.ativo
       and not ms.is_deleted
     limit 1;

    if v_modulo_id is null then
        return new;
    end if;

    -- A projeção só fica habilitada quando contrato, vigência e registro permitem.
    -- SUSPENSO, INADIMPLENTE, CANCELADO e EXPIRADO continuam contratualmente
    -- distinguíveis, mas nunca são convertidos em acesso habilitado.
    v_habilitado := new.ativo
        and new.status in ('TRIAL', 'EM_IMPLANTACAO', 'CONTRATADO', 'HABILITADO', 'ATIVO', 'BETA')
        and (new.vigencia_inicio is null or new.vigencia_inicio <= current_date)
        and (new.vigencia_fim is null or new.vigencia_fim >= current_date)
        and (new.cancelamento_agendado_para is null or new.cancelamento_agendado_para > current_date);

    insert into sigov.tenant_modulo (
        tenant_id, modulo_saas_id, habilitado, contratado, inicio_at, fim_at,
        configuracoes, ativo, created_by, updated_by, correlation_id)
    values (
        new.tenant_id, v_modulo_id, v_habilitado,
        new.ativo and new.status not in ('DISPONIVEL', 'CANCELADO', 'EXPIRADO'),
        coalesce(new.vigencia_inicio, current_date)::timestamptz,
        new.vigencia_fim::timestamptz, new.parametros_json, new.ativo,
        new.created_by, new.updated_by, new.correlation_id)
    on conflict (tenant_id, modulo_saas_id) do update set
        habilitado = excluded.habilitado,
        contratado = excluded.contratado,
        inicio_at = excluded.inicio_at,
        fim_at = excluded.fim_at,
        configuracoes = excluded.configuracoes,
        ativo = excluded.ativo,
        updated_at = now(),
        updated_by = excluded.updated_by,
        correlation_id = excluded.correlation_id;

    return new;
end
$$;

-- Recriar corrige de forma determinística os cenários ausente, desabilitado,
-- associado a função divergente ou instalado com eventos/momento incorretos.
drop trigger if exists trg_tenant_modulo_compatibilizar on sigov.tenant_modulo_contratado;
create trigger trg_tenant_modulo_compatibilizar
after insert or update on sigov.tenant_modulo_contratado
for each row
execute function sigov.fn_tenant_modulo_compatibilizar();
