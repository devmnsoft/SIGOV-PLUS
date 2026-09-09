-- Compatibilidade forward-only das relações operacionais de saneamento com
-- RC50.92. Contexto e vínculos sem fonte persistida permanecem NULL.
do $$
declare
    tabela text;
begin
    foreach tabela in array array['saneamento_ocorrencia','saneamento_ordem_servico'] loop
        if to_regclass('sigov.' || tabela) is null then
            continue;
        end if;
        execute format('alter table sigov.%I add column if not exists exercicio_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists esfera_governo varchar(12)', tabela);
        execute format('alter table sigov.%I add column if not exists tipo_entidade varchar(80)', tabela);
        execute format('alter table sigov.%I add column if not exists orgao_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists orgao_superior_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_gestora_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_executora_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists territorio_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists prioridade varchar(20)', tabela);
        execute format('alter table sigov.%I add column if not exists responsavel_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists data_ocorrencia timestamptz', tabela);
        execute format('alter table sigov.%I add column if not exists equipe_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists previsao timestamptz', tabela);
    end loop;
end $$;

alter table if exists sigov.saneamento_ocorrencia
    add column if not exists protocolo varchar(80);

update sigov.saneamento_ocorrencia
set protocolo = coalesce(nullif(protocolo, ''), nullif(codigo, ''), 'LEGACY-OC-' || id::text)
where protocolo is null or protocolo = '';

with duplicados as (
    select id, row_number() over(partition by tenant_id, entidade_id, protocolo order by id) as ordem
    from sigov.saneamento_ocorrencia
)
update sigov.saneamento_ocorrencia ocorrencia
set protocolo = ocorrencia.protocolo || '-' || ocorrencia.id::text
from duplicados d
where d.id = ocorrencia.id and d.ordem > 1;

alter table if exists sigov.saneamento_ocorrencia
    alter column protocolo set not null;
