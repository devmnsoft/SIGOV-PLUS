-- Saúde/ACS base - schema único sigov, PostgreSQL, Dapper-ready e idempotente.
create extension if not exists pgcrypto;

create or replace function sigov.saude_touch_updated_at() returns trigger language plpgsql as $$
begin
    new.updated_at := now();
    return new;
end $$;

create table if not exists sigov.unidade_saude (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(50) not null,
    nome varchar(250) not null,
    cnes varchar(30) null,
    tipo_unidade varchar(80) not null,
    situacao varchar(40) not null,
    endereco_json jsonb not null default '{}'::jsonb,
    contato_json jsonb not null default '{}'::jsonb,
    latitude numeric(12,8) null,
    longitude numeric(12,8) null,
    observacao text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo)
);

create table if not exists sigov.profissional_saude (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    servidor_id bigint null,
    unidade_saude_id bigint null references sigov.unidade_saude(id),
    codigo_profissional varchar(80) not null,
    cbo varchar(20) null,
    conselho_classe varchar(30) null,
    numero_conselho varchar(60) null,
    uf_conselho varchar(2) null,
    tipo_profissional varchar(80) not null,
    situacao varchar(40) not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo_profissional)
);

create table if not exists sigov.paciente (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    codigo_paciente varchar(80) not null,
    cartao_sus varchar(30) null,
    prontuario_numero varchar(80) null,
    grupo_sanguineo varchar(10) null,
    alergias text null,
    condicoes_cronicas jsonb not null default '[]'::jsonb,
    dados_sensiveis_json jsonb not null default '{}'::jsonb,
    situacao varchar(40) not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo_paciente),
    unique (tenant_id, entidade_id, pessoa_id)
);

create table if not exists sigov.prontuario (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    paciente_id bigint not null references sigov.paciente(id),
    numero varchar(80) not null,
    resumo_clinico text null,
    alergias text null,
    condicoes_cronicas jsonb not null default '[]'::jsonb,
    observacoes_sensiveis text null,
    ultimo_atendimento_at timestamptz null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, paciente_id),
    unique (tenant_id, entidade_id, numero)
);

create table if not exists sigov.atendimento_saude (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    unidade_saude_id bigint not null references sigov.unidade_saude(id),
    paciente_id bigint not null references sigov.paciente(id),
    profissional_saude_id bigint null references sigov.profissional_saude(id),
    numero varchar(80) not null,
    data_atendimento timestamptz not null default now(),
    tipo_atendimento varchar(80) not null,
    classificacao_risco varchar(40) null,
    queixa_principal text null,
    conduta text null,
    cid10 varchar(20) null,
    status varchar(40) not null,
    dados_clinicos_json jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, numero),
    check (status in ('AGENDADO','EM_ATENDIMENTO','ATENDIDO','CANCELADO','FALTOU'))
);

create table if not exists sigov.agenda_saude (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    unidade_saude_id bigint not null references sigov.unidade_saude(id), profissional_saude_id bigint null references sigov.profissional_saude(id), paciente_id bigint null references sigov.paciente(id),
    data_inicio timestamptz not null, data_fim timestamptz not null, tipo_agendamento varchar(80) not null, status varchar(40) not null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    check (data_inicio < data_fim)
);

create table if not exists sigov.farmacia_produto (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(80) not null, nome varchar(250) not null, principio_ativo varchar(250) null, concentracao varchar(100) null, forma_farmaceutica varchar(100) null, unidade_medida varchar(40) not null,
    controla_lote boolean not null default true, medicamento_controlado boolean not null default false,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo)
);

create table if not exists sigov.farmacia_estoque (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), unidade_saude_id bigint not null references sigov.unidade_saude(id), farmacia_produto_id bigint not null references sigov.farmacia_produto(id),
    lote varchar(80) null, validade date null, quantidade numeric(18,4) not null default 0,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, unidade_saude_id, farmacia_produto_id, lote), check (quantidade >= 0)
);

create table if not exists sigov.farmacia_dispensacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), unidade_saude_id bigint not null references sigov.unidade_saude(id), paciente_id bigint not null references sigov.paciente(id), farmacia_produto_id bigint not null references sigov.farmacia_produto(id), profissional_saude_id bigint null references sigov.profissional_saude(id),
    data_dispensacao timestamptz not null default now(), quantidade numeric(18,4) not null, lote varchar(80) null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    check (quantidade > 0)
);

create table if not exists sigov.vacinacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), unidade_saude_id bigint not null references sigov.unidade_saude(id), paciente_id bigint not null references sigov.paciente(id), profissional_saude_id bigint null references sigov.profissional_saude(id),
    vacina varchar(150) not null, dose varchar(50) not null, lote varchar(80) null, data_aplicacao date not null, fabricante varchar(150) null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.laboratorio_exame (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), paciente_id bigint not null references sigov.paciente(id), unidade_saude_id bigint null references sigov.unidade_saude(id), profissional_solicitante_id bigint null references sigov.profissional_saude(id),
    tipo_exame varchar(150) not null, data_solicitacao date not null default current_date, data_resultado date null, status varchar(40) not null, resultado_json jsonb not null default '{}'::jsonb, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.regulacao_solicitacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), paciente_id bigint not null references sigov.paciente(id), unidade_origem_id bigint null references sigov.unidade_saude(id),
    tipo_solicitacao varchar(100) not null, especialidade varchar(150) null, prioridade varchar(40) not null, justificativa text not null, status varchar(40) not null, data_solicitacao date not null default current_date,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.acs_microarea (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), unidade_saude_id bigint not null references sigov.unidade_saude(id), codigo varchar(50) not null, nome varchar(150) not null, profissional_acs_id bigint null references sigov.profissional_saude(id), poligono_geojson jsonb null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo)
);

create table if not exists sigov.acs_dispositivo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), profissional_acs_id bigint not null references sigov.profissional_saude(id), identificador varchar(150) not null, modelo varchar(150) null, plataforma varchar(80) null, ultimo_sync_at timestamptz null, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, identificador)
);

create table if not exists sigov.acs_cadastro_domiciliar (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), acs_microarea_id bigint null references sigov.acs_microarea(id), codigo_domicilio varchar(80) not null, endereco_json jsonb not null default '{}'::jsonb, condicoes_moradia_json jsonb not null default '{}'::jsonb,
    latitude numeric(12,8) null, longitude numeric(12,8) null, precisao_metros numeric(12,4) null, data_cadastro date not null default current_date, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo_domicilio), check ((latitude is null and longitude is null) or (latitude between -90 and 90 and longitude between -180 and 180))
);

create table if not exists sigov.acs_cadastro_individual (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), acs_cadastro_domiciliar_id bigint null references sigov.acs_cadastro_domiciliar(id), paciente_id bigint null references sigov.paciente(id), pessoa_id bigint not null references sigov.pessoa(id),
    condicoes_saude_json jsonb not null default '{}'::jsonb, vulnerabilidades_json jsonb not null default '{}'::jsonb, data_cadastro date not null default current_date, status varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.acs_visita (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), profissional_acs_id bigint not null references sigov.profissional_saude(id), acs_cadastro_domiciliar_id bigint null references sigov.acs_cadastro_domiciliar(id), acs_cadastro_individual_id bigint null references sigov.acs_cadastro_individual(id), paciente_id bigint null references sigov.paciente(id),
    data_visita timestamptz not null default now(), tipo_visita varchar(80) not null, desfecho varchar(80) not null, observacao text null, latitude numeric(12,8) null, longitude numeric(12,8) null, precisao_metros numeric(12,4) null, offline_id varchar(120) null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    check (acs_cadastro_domiciliar_id is not null or acs_cadastro_individual_id is not null or paciente_id is not null), check ((latitude is null and longitude is null) or (latitude between -90 and 90 and longitude between -180 and 180))
);

create table if not exists sigov.acs_atividade_coletiva (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id), unidade_saude_id bigint null references sigov.unidade_saude(id), profissional_acs_id bigint not null references sigov.profissional_saude(id), data_atividade date not null default current_date, tema varchar(150) not null, publico_alvo varchar(150) null, quantidade_participantes int not null default 0, latitude numeric(12,8) null, longitude numeric(12,8) null, precisao_metros numeric(12,4) null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.acs_sync_lote (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), acs_dispositivo_id bigint null references sigov.acs_dispositivo(id), profissional_acs_id bigint null references sigov.profissional_saude(id), lote_id varchar(120) not null, status varchar(40) not null, recebido_at timestamptz not null default now(), processado_at timestamptz null, total_itens int not null default 0, total_processados int not null default 0, total_erros int not null default 0, payload jsonb not null default '{}'::jsonb, erro text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, lote_id)
);

create table if not exists sigov.acs_sync_item (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), acs_sync_lote_id bigint not null references sigov.acs_sync_lote(id), tipo_item varchar(80) not null, offline_id varchar(120) not null, status varchar(40) not null, payload jsonb not null default '{}'::jsonb, erro text null, processado_at timestamptz null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, acs_sync_lote_id, offline_id)
);

create table if not exists sigov.saude_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), tipo_evento varchar(120) not null, aggregate_type varchar(120) not null, aggregate_id bigint not null, payload jsonb not null default '{}'::jsonb, publicado boolean not null default false, publicado_at timestamptz null, erro text null,
    ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create index if not exists idx_unidade_saude_tenant_codigo on sigov.unidade_saude (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_profissional_saude_tenant_pessoa on sigov.profissional_saude (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_paciente_tenant_codigo on sigov.paciente (tenant_id, entidade_id, codigo_paciente) where is_deleted = false;
create index if not exists idx_paciente_tenant_pessoa on sigov.paciente (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_prontuario_tenant_paciente on sigov.prontuario (tenant_id, entidade_id, paciente_id) where is_deleted = false;
create index if not exists idx_atendimento_tenant_paciente on sigov.atendimento_saude (tenant_id, entidade_id, paciente_id) where is_deleted = false;
create index if not exists idx_atendimento_tenant_unidade_data on sigov.atendimento_saude (tenant_id, entidade_id, unidade_saude_id, data_atendimento) where is_deleted = false;
create index if not exists idx_agenda_tenant_unidade_data on sigov.agenda_saude (tenant_id, entidade_id, unidade_saude_id, data_inicio) where is_deleted = false;
create index if not exists idx_farmacia_produto_tenant_codigo on sigov.farmacia_produto (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_farmacia_estoque_tenant_produto on sigov.farmacia_estoque (tenant_id, entidade_id, farmacia_produto_id);
create index if not exists idx_farmacia_dispensacao_tenant_paciente on sigov.farmacia_dispensacao (tenant_id, entidade_id, paciente_id) where is_deleted = false;
create index if not exists idx_vacinacao_tenant_paciente on sigov.vacinacao (tenant_id, entidade_id, paciente_id) where is_deleted = false;
create index if not exists idx_laboratorio_exame_tenant_paciente on sigov.laboratorio_exame (tenant_id, entidade_id, paciente_id) where is_deleted = false;
create index if not exists idx_regulacao_tenant_status on sigov.regulacao_solicitacao (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_acs_microarea_tenant_codigo on sigov.acs_microarea (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_acs_domicilio_tenant_codigo on sigov.acs_cadastro_domiciliar (tenant_id, entidade_id, codigo_domicilio) where is_deleted = false;
create index if not exists idx_acs_individual_tenant_pessoa on sigov.acs_cadastro_individual (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_acs_visita_tenant_data on sigov.acs_visita (tenant_id, entidade_id, data_visita) where is_deleted = false;
create index if not exists idx_acs_visita_tenant_acs on sigov.acs_visita (tenant_id, entidade_id, profissional_acs_id) where is_deleted = false;
create index if not exists idx_acs_sync_lote_tenant_lote on sigov.acs_sync_lote (tenant_id, entidade_id, lote_id) where is_deleted = false;
create index if not exists idx_acs_sync_item_tenant_offline on sigov.acs_sync_item (tenant_id, entidade_id, offline_id) where is_deleted = false;

create or replace view sigov.vw_saude_dashboard as
select tenant_id, entidade_id, count(*) filter (where tabela='paciente') as total_pacientes
from (
    select tenant_id, entidade_id, 'paciente' as tabela from sigov.paciente where is_deleted = false
    union all select tenant_id, entidade_id, 'unidade' from sigov.unidade_saude where is_deleted = false
) x group by tenant_id, entidade_id;

create or replace view sigov.vw_saude_atendimentos_resumo as
select tenant_id, entidade_id, status, date_trunc('day', data_atendimento)::date as data, count(*) as total
from sigov.atendimento_saude where is_deleted = false group by tenant_id, entidade_id, status, date_trunc('day', data_atendimento);

create or replace view sigov.vw_saude_farmacia_estoque_baixo as
select tenant_id, entidade_id, unidade_saude_id, farmacia_produto_id, lote, validade, quantidade
from sigov.farmacia_estoque where quantidade <= 10 and is_deleted = false;

create or replace view sigov.vw_saude_acs_visitas_resumo as
select tenant_id, entidade_id, profissional_acs_id, date_trunc('month', data_visita)::date as mes, count(*) as total
from sigov.acs_visita where is_deleted = false group by tenant_id, entidade_id, profissional_acs_id, date_trunc('month', data_visita);

create or replace view sigov.vw_saude_vacinacao_resumo as
select tenant_id, entidade_id, vacina, dose, date_trunc('month', data_aplicacao)::date as mes, count(*) as total
from sigov.vacinacao where is_deleted = false group by tenant_id, entidade_id, vacina, dose, date_trunc('month', data_aplicacao);

create or replace view sigov.vw_saude_regulacao_resumo as
select tenant_id, entidade_id, status, prioridade, count(*) as total
from sigov.regulacao_solicitacao where is_deleted = false group by tenant_id, entidade_id, status, prioridade;

do $$
declare t text;
begin
    foreach t in array array['unidade_saude','profissional_saude','paciente','prontuario','atendimento_saude','agenda_saude','farmacia_produto','farmacia_estoque','farmacia_dispensacao','vacinacao','laboratorio_exame','regulacao_solicitacao','acs_microarea','acs_dispositivo','acs_cadastro_domiciliar','acs_cadastro_individual','acs_visita','acs_atividade_coletiva','acs_sync_lote','acs_sync_item','saude_evento'] loop
        execute format('drop trigger if exists trg_%s_touch on sigov.%I', t, t);
        execute format('create trigger trg_%s_touch before update on sigov.%I for each row execute function sigov.saude_touch_updated_at()', t, t);
        execute format('alter table sigov.%I enable row level security', t);
        execute format('drop policy if exists %I on sigov.%I', 'rls_' || t || '_tenant_isolation', t);
        execute format('create policy %I on sigov.%I using (tenant_id = sigov.current_tenant_id()) with check (tenant_id = sigov.current_tenant_id())', 'rls_' || t || '_tenant_isolation', t);
    end loop;
end $$;

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo)
values ('saude', 'Saúde', 'Módulo base de saúde, ACS, pacientes, atendimentos, farmácia e vacinação do sigov', 'Operacional', 50, '/Saude/Dashboard', 'heart-pulse', true)
on conflict (codigo) do nothing;

insert into sigov.feature_flag_def (codigo, nome, descricao, modulo, ativo)
values ('saude.acs_offline', 'ACS offline estrutural', 'Habilita endpoints e tela de sincronização offline estrutural do ACS.', 'saude', true)
on conflict (codigo) do nothing;

insert into sigov.tenant_modulo (tenant_id, modulo_saas_id, habilitado, contratado, ativo)
select t.id, m.id, true, true, true
from sigov.tenant t
join sigov.modulo_saas m on m.codigo = 'saude'
where t.is_deleted = false
on conflict (tenant_id, modulo_saas_id) do nothing;

insert into sigov.tenant_feature_flag (tenant_id, feature_flag_def_id, habilitado, ativo)
select t.id, f.id, true, true
from sigov.tenant t
join sigov.feature_flag_def f on f.codigo = 'saude.acs_offline'
where t.is_deleted = false
on conflict (tenant_id, feature_flag_def_id) do nothing;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
('saude','unidade','visualizar','saude.unidade.visualizar','Visualizar unidades de saúde',true),('saude','unidade','criar','saude.unidade.criar','Criar unidades de saúde',true),('saude','unidade','editar','saude.unidade.editar','Editar unidades de saúde',true),('saude','unidade','excluir','saude.unidade.excluir','Excluir unidades de saúde',true),
('saude','profissional','visualizar','saude.profissional.visualizar','Visualizar profissionais de saúde',true),('saude','profissional','criar','saude.profissional.criar','Criar profissionais de saúde',true),('saude','profissional','editar','saude.profissional.editar','Editar profissionais de saúde',true),('saude','profissional','excluir','saude.profissional.excluir','Excluir profissionais de saúde',true),
('saude','paciente','visualizar','saude.paciente.visualizar','Visualizar pacientes',true),('saude','paciente','criar','saude.paciente.criar','Criar pacientes',true),('saude','paciente','editar','saude.paciente.editar','Editar pacientes',true),('saude','paciente','excluir','saude.paciente.excluir','Excluir pacientes',true),('saude','paciente','visualizar_dados_completos','saude.paciente.visualizar_dados_completos','Visualizar dados completos de paciente',true),
('saude','prontuario','visualizar','saude.prontuario.visualizar','Visualizar prontuário',true),('saude','prontuario','editar','saude.prontuario.editar','Editar prontuário',true),('saude','prontuario','visualizar_dados_sensiveis','saude.prontuario.visualizar_dados_sensiveis','Visualizar dados sensíveis de prontuário',true),
('saude','atendimento','visualizar','saude.atendimento.visualizar','Visualizar atendimentos',true),('saude','atendimento','criar','saude.atendimento.criar','Criar atendimentos',true),('saude','atendimento','editar','saude.atendimento.editar','Editar atendimentos',true),('saude','atendimento','cancelar','saude.atendimento.cancelar','Cancelar atendimentos',true),
('saude','agenda','visualizar','saude.agenda.visualizar','Visualizar agenda',true),('saude','agenda','criar','saude.agenda.criar','Criar agenda',true),('saude','agenda','cancelar','saude.agenda.cancelar','Cancelar agenda',true),
('saude','farmacia','visualizar','saude.farmacia.visualizar','Visualizar farmácia',true),('saude','farmacia','produto.criar','saude.farmacia.produto.criar','Criar produto de farmácia',true),('saude','farmacia','dispensar','saude.farmacia.dispensar','Dispensar medicamento',true),
('saude','vacinacao','visualizar','saude.vacinacao.visualizar','Visualizar vacinação',true),('saude','vacinacao','criar','saude.vacinacao.criar','Registrar vacinação',true),
('saude','laboratorio','visualizar','saude.laboratorio.visualizar','Visualizar laboratório',true),('saude','laboratorio','criar','saude.laboratorio.criar','Criar exame',true),('saude','laboratorio','resultado','saude.laboratorio.resultado','Registrar resultado de exame',true),
('saude','regulacao','visualizar','saude.regulacao.visualizar','Visualizar regulação',true),('saude','regulacao','criar','saude.regulacao.criar','Criar regulação',true),('saude','regulacao','editar','saude.regulacao.editar','Editar regulação',true),
('saude','acs','visualizar','saude.acs.visualizar','Visualizar ACS',true),('saude','acs','cadastrar','saude.acs.cadastrar','Cadastrar dados ACS',true),('saude','acs','visita','saude.acs.visita','Registrar visita ACS',true),('saude','acs','sync','saude.acs.sync','Sincronizar ACS',true),
('saude','dashboard','visualizar','saude.dashboard.visualizar','Visualizar dashboard saúde',true),('saude','exportar','exportar','saude.exportar','Exportar dados de saúde',true)
on conflict do nothing;

do $$
declare v_tenant bigint; v_entidade bigint; v_unidade bigint; v_produto bigint;
begin
    select id into v_tenant from sigov.tenant where is_deleted=false order by id limit 1;
    select id into v_entidade from sigov.entidade where is_deleted=false order by id limit 1;
    if v_tenant is not null and v_entidade is not null then
        insert into sigov.unidade_saude (tenant_id, entidade_id, codigo, nome, cnes, tipo_unidade, situacao, endereco_json, contato_json)
        values (v_tenant, v_entidade, 'UBS-DEMO', 'Unidade Básica de Saúde Demo', null, 'BASICA', 'ATIVA', '{}'::jsonb, '{}'::jsonb)
        on conflict (tenant_id, entidade_id, codigo) do nothing;
        select id into v_unidade from sigov.unidade_saude where tenant_id=v_tenant and entidade_id=v_entidade and codigo='UBS-DEMO';
        insert into sigov.farmacia_produto (tenant_id, entidade_id, codigo, nome, principio_ativo, unidade_medida)
        values (v_tenant, v_entidade, 'MED-DEMO', 'Medicamento Demo', 'Princípio ativo demo', 'UN')
        on conflict (tenant_id, entidade_id, codigo) do nothing;
        select id into v_produto from sigov.farmacia_produto where tenant_id=v_tenant and entidade_id=v_entidade and codigo='MED-DEMO';
        if v_unidade is not null and v_produto is not null then
            insert into sigov.farmacia_estoque (tenant_id, entidade_id, unidade_saude_id, farmacia_produto_id, lote, quantidade)
            values (v_tenant, v_entidade, v_unidade, v_produto, 'LOTE-DEMO', 100)
            on conflict do nothing;
            insert into sigov.acs_microarea (tenant_id, entidade_id, unidade_saude_id, codigo, nome)
            values (v_tenant, v_entidade, v_unidade, 'MA-DEMO', 'Microárea Demo')
            on conflict (tenant_id, entidade_id, codigo) do nothing;
        end if;
    end if;
end $$;
