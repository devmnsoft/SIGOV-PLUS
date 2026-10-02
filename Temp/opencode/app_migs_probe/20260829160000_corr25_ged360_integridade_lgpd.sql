-- CORR25 GED360/InovaGED: integridade, idempotência e bloqueios documentais.
-- Migration corretiva idempotente; nenhuma estrutura publicada é removida.

-- Hashes SHA-256 persistidos devem ter representação hexadecimal completa.
do $$
begin
    if to_regclass('sigov.ged_documento_arquivo') is not null then
        begin
            alter table sigov.ged_documento_arquivo
                add constraint ck_ged_documento_arquivo_sha256
                check (hash_sha256 ~ '^[0-9a-fA-F]{64}$') not valid;
        exception when duplicate_object then null;
        end;
    end if;

    if to_regclass('sigov.ged_lote_importacao_item') is not null then
        begin
            alter table sigov.ged_lote_importacao_item
                add constraint ck_ged_importacao_item_sha256
                check (hash_sha256 is null or hash_sha256 ~ '^[0-9a-fA-F]{64}$') not valid;
        exception when duplicate_object then null;
        end;
    end if;

    if to_regclass('sigov.ged_assinatura_solicitacao') is not null then
        begin
            alter table sigov.ged_assinatura_solicitacao
                add constraint ck_ged_assinatura_sha256
                check (hash_documento is null or hash_documento ~ '^[0-9a-fA-F]{64}$') not valid;
        exception when duplicate_object then null;
        end;
    end if;
end
$$;

-- Datas incoerentes e empréstimos sem objeto documental/físico são recusados.
do $$
begin
    if to_regclass('sigov.ged_emprestimo') is not null then
        begin
            alter table sigov.ged_emprestimo
                add constraint ck_ged_emprestimo_objeto
                check (num_nonnulls(caixa_id, documento_id) = 1) not valid;
        exception when duplicate_object then null;
        end;
        begin
            alter table sigov.ged_emprestimo
                add constraint ck_ged_emprestimo_datas
                check (previsto_para >= emprestado_em and (devolvido_em is null or devolvido_em >= emprestado_em)) not valid;
        exception when duplicate_object then null;
        end;
    end if;

    if to_regclass('sigov.ged_assinatura_solicitacao') is not null then
        begin
            alter table sigov.ged_assinatura_solicitacao
                add constraint ck_ged_assinatura_conclusao
                check ((status = 'ASSINADA' and assinado_em is not null and provedor is not null and hash_documento is not null)
                    or (status <> 'ASSINADA' and assinado_em is null)) not valid;
        exception when duplicate_object then null;
        end;
    end if;

    if to_regclass('sigov.ged_eliminacao_lote') is not null then
        begin
            alter table sigov.ged_eliminacao_lote
                add constraint ck_ged_eliminacao_aprovacao
                check ((status in ('APROVADO','EXECUTADO') and aprovado_por is not null and aprovado_em is not null)
                    or status not in ('APROVADO','EXECUTADO')) not valid;
        exception when duplicate_object then null;
        end;
        begin
            alter table sigov.ged_eliminacao_lote
                add constraint ck_ged_eliminacao_execucao
                check ((status = 'EXECUTADO' and executado_em is not null) or (status <> 'EXECUTADO' and executado_em is null)) not valid;
        exception when duplicate_object then null;
        end;
    end if;
end
$$;

-- Duplicidade documental/importação é detectável por contexto, sem alterar dados legados.
create unique index if not exists ux_ged_documento_arquivo_hash_ativo
    on sigov.ged_documento_arquivo (tenant_id, hash_sha256)
    where ativo and principal;
create unique index if not exists ux_ged_importacao_item_hash_ativo
    on sigov.ged_lote_importacao_item (tenant_id, hash_sha256)
    where ativo and hash_sha256 is not null and status <> 'DUPLICADO';
create unique index if not exists ux_ged_busca_salva_usuario_nome
    on sigov.ged_busca_salva (tenant_id, usuario_id, nome)
    where ativo;

-- O banco impede eliminação executada com hold jurídico/auditoria ou item não elegível.
create or replace function sigov.fn_ged_validar_eliminacao_lote()
returns trigger
language plpgsql
as $$
begin
    if new.status = 'EXECUTADO' and old.status is distinct from 'EXECUTADO' then
        if old.status <> 'APROVADO' then
            raise exception 'O lote de eliminação precisa estar aprovado antes da execução.';
        end if;
        if exists (
            select 1
              from sigov.ged_eliminacao_item i
             where i.tenant_id = new.tenant_id
               and i.lote_id = new.id
               and i.ativo
               and (i.hold_juridico or i.status <> 'ELEGIVEL')
        ) then
            raise exception 'Eliminação bloqueada por hold jurídico/auditoria ou item não elegível.';
        end if;
    end if;
    return new;
end
$$;

do $$
begin
    if to_regclass('sigov.ged_eliminacao_lote') is not null
       and not exists (select 1 from pg_trigger where tgname = 'tr_ged_validar_eliminacao_lote') then
        create trigger tr_ged_validar_eliminacao_lote
        before update of status on sigov.ged_eliminacao_lote
        for each row execute function sigov.fn_ged_validar_eliminacao_lote();
    end if;
end
$$;

comment on function sigov.fn_ged_validar_eliminacao_lote() is
'CORR25: bloqueio fail-closed de execução sem aprovação, com hold ou item não elegível.';
