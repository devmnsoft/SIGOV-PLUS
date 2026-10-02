-- Correção: separa o contrato UUID legado de devolução do nome canônico reservado à jornada
-- física (bigint identity). O bootstrap 070 renomeou sigov.compras_devolucao para
-- sigov.compras_empresarial_devolucao antes deste ponto; 20260924210000 exige esse nome para o
-- contrato canônico. O legado é preservado por rename, sem conversão destrutiva, para o alias
-- sigov.compras_empresarial_devolucao_legado; forma não reconhecida falha explicitamente.
do $$
begin
    if to_regclass('sigov.compras_empresarial_devolucao') is not null then
        if exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_devolucao' and column_name='situacao') then
            return;
        end if;
        if not exists(select 1 from pg_attribute a where a.attrelid='sigov.compras_empresarial_devolucao'::regclass and a.attname='id' and a.atttypid='uuid'::regtype and not a.attisdropped) then
            raise exception using errcode='55000', message='sigov.compras_empresarial_devolucao apresenta forma nem canônica (sem coluna situacao) nem legado (PK uuid); reconciliação manual exigida.';
        end if;
        if to_regclass('sigov.compras_empresarial_devolucao_legado') is not null then
            raise exception using errcode='55000', message='sigov.compras_empresarial_devolucao e sigov.compras_empresarial_devolucao_legado coexistem; reconciliação manual exigida.';
        end if;
        alter table sigov.compras_empresarial_devolucao rename to compras_empresarial_devolucao_legado;
    else
        if to_regclass('sigov.compras_devolucao') is not null then
            if to_regclass('sigov.compras_empresarial_devolucao_legado') is not null then
                raise exception using errcode='55000', message='sigov.compras_devolucao e sigov.compras_empresarial_devolucao_legado coexistem; reconciliação manual exigida.';
            end if;
            alter table sigov.compras_devolucao rename to compras_empresarial_devolucao_legado;
        end if;
    end if;
    if to_regclass('sigov.compras_devolucao') is not null then
        raise exception using errcode='55000', message='Contrato legado sigov.compras_devolucao ainda presente após a separação; reconciliação manual exigida.';
    end if;
end $$;
