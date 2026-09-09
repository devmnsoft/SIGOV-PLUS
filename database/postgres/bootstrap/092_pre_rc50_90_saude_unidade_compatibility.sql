-- Compatibilidade forward-only da unidade de saúde legada com Saúde360.
-- As colunas contextuais e operacionais são aditivas; vínculos inexistentes
-- permanecem NULL para não simular autoridade clínica ou administrativa.
alter table if exists sigov.saude_unidade
    add column if not exists exercicio_id bigint,
    add column if not exists esfera_governo varchar(12),
    add column if not exists tipo_entidade varchar(60),
    add column if not exists orgao_superior_id bigint,
    add column if not exists orgao_id bigint,
    add column if not exists unidade_gestora_id bigint,
    add column if not exists unidade_executora_id bigint,
    add column if not exists hierarquia_administrativa varchar(200),
    add column if not exists abrangencia_territorial varchar(160),
    add column if not exists uf char(2),
    add column if not exists municipio varchar(160),
    add column if not exists regiao varchar(120),
    add column if not exists jurisdicao varchar(160),
    add column if not exists unidade_saude_id bigint,
    add column if not exists paciente_id bigint,
    add column if not exists profissional_id bigint,
    add column if not exists equipe_id bigint,
    add column if not exists territorio_id bigint,
    add column if not exists procedimento_id bigint,
    add column if not exists prioridade varchar(20),
    add column if not exists data_inicio timestamptz,
    add column if not exists data_fim timestamptz,
    add column if not exists quantidade numeric(18,3),
    add column if not exists valor numeric(18,2),
    add column if not exists percentual numeric(7,4),
    add column if not exists validade date,
    add column if not exists dados jsonb not null default '{}'::jsonb;

do $$
declare
    tabela text;
begin
    foreach tabela in array array[
        'saude_acs_microarea', 'saude_acs_visita', 'saude_agenda', 'saude_atendimento',
        'saude_encaminhamento', 'saude_equipe', 'saude_farmacia_dispensacao',
        'saude_farmacia_estoque', 'saude_farmacia_lote', 'saude_farmacia_movimento',
        'saude_paciente', 'saude_procedimento', 'saude_profissional',
        'saude_regulacao_solicitacao', 'saude_unidade', 'saude_unidade_servico',
        'saude_vigilancia_notificacao'
    ] loop
        if to_regclass('sigov.' || tabela) is null then
            continue;
        end if;
        execute format('alter table sigov.%I add column if not exists exercicio_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists esfera_governo varchar(12)', tabela);
        execute format('alter table sigov.%I add column if not exists tipo_entidade varchar(60)', tabela);
        execute format('alter table sigov.%I add column if not exists orgao_superior_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists orgao_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_gestora_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_executora_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists unidade_saude_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists paciente_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists profissional_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists equipe_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists territorio_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists procedimento_id bigint', tabela);
        execute format('alter table sigov.%I add column if not exists prioridade varchar(20)', tabela);
        execute format('alter table sigov.%I add column if not exists data_inicio timestamptz', tabela);
    end loop;
end $$;
