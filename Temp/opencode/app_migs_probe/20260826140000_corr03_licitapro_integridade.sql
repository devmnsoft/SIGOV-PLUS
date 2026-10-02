-- CORR03 - fechamento do LicitaPro IA no FUNC03.
-- Migration corretiva aditiva: a EXP03 publicada permanece imutável.

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'ck_clp_fonte_endpoint_url') then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_clp_doc_referencia_preenchida') then
        alter table sigov.compras_licitapro_documento
            add constraint ck_clp_doc_referencia_preenchida
            check (status <> 'APROVADO' or nullif(btrim(referencia_documental), '') is not null) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_clp_criterio_explicacao_preenchida') then
        alter table sigov.compras_licitapro_criterio
            add constraint ck_clp_criterio_explicacao_preenchida
            check (nullif(btrim(explicacao), '') is not null) not valid;
    end if;
end $$;

create index if not exists ix_clp_importacao_tenant_fonte
    on sigov.compras_licitapro_importacao(tenant_id, entidade_id, fonte_id, iniciada_at desc);
create index if not exists ix_clp_item_tenant_checklist_status
    on sigov.compras_licitapro_checklist_item(tenant_id, entidade_id, checklist_id, status);
create index if not exists ix_clp_analise_tenant_processo
    on sigov.compras_licitapro_analise(tenant_id, entidade_id, processo_id, status);
create index if not exists ix_clp_criterio_tenant_analise
    on sigov.compras_licitapro_criterio(tenant_id, entidade_id, analise_id);
create index if not exists ix_clp_alerta_tenant_status_vencimento
    on sigov.compras_licitapro_alerta(tenant_id, entidade_id, status, vencimento_at);

create or replace function sigov.compras_licitapro_validar_relacoes()
returns trigger language plpgsql as $$
begin
    if tg_table_name = 'compras_licitapro_checklist_item' and not exists (
        select 1 from sigov.compras_licitapro_checklist x where x.id=new.checklist_id and x.tenant_id=new.tenant_id and x.entidade_id=new.entidade_id
    ) then raise exception 'Checklist fora do contexto tenant/entidade';
    elsif tg_table_name = 'compras_licitapro_criterio' and not exists (
        select 1 from sigov.compras_licitapro_analise x where x.id=new.analise_id and x.tenant_id=new.tenant_id and x.entidade_id=new.entidade_id
    ) then raise exception 'Análise fora do contexto tenant/entidade';
    end if;
    return new;
end $$;

drop trigger if exists trg_clp_checklist_item_contexto on sigov.compras_licitapro_checklist_item;
create trigger trg_clp_checklist_item_contexto before insert or update on sigov.compras_licitapro_checklist_item
for each row execute function sigov.compras_licitapro_validar_relacoes();
drop trigger if exists trg_clp_criterio_contexto on sigov.compras_licitapro_criterio;
create trigger trg_clp_criterio_contexto before insert or update on sigov.compras_licitapro_criterio
for each row execute function sigov.compras_licitapro_validar_relacoes();
