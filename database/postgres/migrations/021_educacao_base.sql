-- Educação Base: escolas, ano letivo, cursos, turmas, alunos, matrículas, frequência, avaliações,
-- pré-matrícula, Educacenso estrutural, portal educacional, dashboard, auditoria técnica e outbox.
-- Todas as estruturas operacionais permanecem no schema único sigov.

create or replace function sigov.educacao_touch_updated_at()
returns trigger language plpgsql as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create table if not exists sigov.escola (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(50) not null,
    nome varchar(250) not null,
    inep_codigo varchar(30) null,
    tipo_escola varchar(40) not null,
    situacao varchar(40) not null,
    endereco_json jsonb not null default '{}'::jsonb,
    contato_json jsonb not null default '{}'::jsonb,
    diretor_pessoa_id bigint null references sigov.pessoa(id),
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

create table if not exists sigov.ano_letivo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    escola_id bigint null references sigov.escola(id),
    ano int not null,
    data_inicio date not null,
    data_fim date not null,
    status varchar(40) not null,
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
    check (data_inicio <= data_fim)
);

create table if not exists sigov.curso (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(50) not null,
    nome varchar(250) not null,
    etapa_ensino varchar(80) not null,
    modalidade varchar(80) null,
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

create table if not exists sigov.serie_ano (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    curso_id bigint not null references sigov.curso(id),
    codigo varchar(50) not null,
    nome varchar(150) not null,
    ordem int not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, curso_id, codigo)
);

create table if not exists sigov.turma (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    escola_id bigint not null references sigov.escola(id),
    ano_letivo_id bigint not null references sigov.ano_letivo(id),
    curso_id bigint not null references sigov.curso(id),
    serie_ano_id bigint not null references sigov.serie_ano(id),
    codigo varchar(50) not null,
    nome varchar(150) not null,
    turno varchar(40) not null,
    capacidade int not null default 0,
    vagas_ocupadas int not null default 0,
    status varchar(40) not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    check (capacidade >= 0),
    check (vagas_ocupadas >= 0 and vagas_ocupadas <= capacidade),
    unique (tenant_id, entidade_id, ano_letivo_id, codigo)
);

create table if not exists sigov.aluno (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    codigo_aluno varchar(80) not null,
    nis varchar(30) null,
    cartao_sus varchar(30) null,
    necessidade_especial boolean not null default false,
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
    unique (tenant_id, entidade_id, codigo_aluno),
    unique (tenant_id, entidade_id, pessoa_id)
);

create table if not exists sigov.responsavel_aluno (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    aluno_id bigint not null references sigov.aluno(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    parentesco varchar(60) not null,
    responsavel_legal boolean not null default false,
    financeiro boolean not null default false,
    autorizado_buscar boolean not null default false,
    contato_emergencia boolean not null default false,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, aluno_id, pessoa_id)
);

create table if not exists sigov.professor (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    servidor_id bigint null references sigov.servidor(id),
    codigo_professor varchar(80) not null,
    formacao varchar(250) null,
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
    unique (tenant_id, entidade_id, codigo_professor),
    unique (tenant_id, entidade_id, pessoa_id)
);

create table if not exists sigov.matricula (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    aluno_id bigint not null references sigov.aluno(id),
    escola_id bigint not null references sigov.escola(id),
    ano_letivo_id bigint not null references sigov.ano_letivo(id),
    turma_id bigint not null references sigov.turma(id),
    numero_matricula varchar(80) not null,
    data_matricula date not null default current_date,
    status varchar(40) not null,
    origem varchar(60) null,
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
    unique (tenant_id, entidade_id, numero_matricula)
);

create table if not exists sigov.professor_turma (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    professor_id bigint not null references sigov.professor(id),
    turma_id bigint not null references sigov.turma(id),
    componente_curricular varchar(150) not null,
    carga_horaria_semanal numeric(9,2) null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.diario_frequencia (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    turma_id bigint not null references sigov.turma(id),
    aluno_id bigint not null references sigov.aluno(id),
    professor_id bigint null references sigov.professor(id),
    data_aula date not null,
    componente_curricular varchar(150) null,
    presente boolean not null default true,
    justificativa text null,
    registrado_by bigint null references sigov.usuario(id),
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.avaliacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    turma_id bigint not null references sigov.turma(id),
    professor_id bigint null references sigov.professor(id),
    componente_curricular varchar(150) not null,
    titulo varchar(150) not null,
    data_avaliacao date not null,
    valor_maximo numeric(9,2) not null default 10,
    peso numeric(9,2) not null default 1,
    status varchar(40) not null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    check (valor_maximo > 0 and peso > 0)
);

create table if not exists sigov.nota (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    avaliacao_id bigint not null references sigov.avaliacao(id),
    aluno_id bigint not null references sigov.aluno(id),
    valor numeric(9,2) not null,
    observacao text null,
    registrado_by bigint null references sigov.usuario(id),
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    check (valor >= 0),
    unique (tenant_id, entidade_id, avaliacao_id, aluno_id)
);

create table if not exists sigov.pre_matricula_inscricao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    escola_preferencial_id bigint null references sigov.escola(id),
    aluno_pessoa_id bigint not null references sigov.pessoa(id),
    responsavel_pessoa_id bigint null references sigov.pessoa(id),
    protocolo varchar(80) not null,
    ano_letivo int not null,
    etapa_ensino varchar(80) not null,
    status varchar(40) not null,
    pontuacao numeric(9,2) null,
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
    unique (tenant_id, entidade_id, protocolo)
);

create table if not exists sigov.educacenso_registro (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    escola_id bigint null references sigov.escola(id),
    aluno_id bigint null references sigov.aluno(id),
    turma_id bigint null references sigov.turma(id),
    tipo_registro varchar(80) not null,
    status varchar(40) not null,
    payload jsonb not null default '{}'::jsonb,
    erro text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.portal_educacao_acesso (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint null references sigov.pessoa(id),
    aluno_id bigint null references sigov.aluno(id),
    acao varchar(80) not null,
    ip varchar(80) null,
    user_agent varchar(250) null,
    metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.educacao_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    tipo_evento varchar(120) not null,
    agregacao varchar(80) not null,
    agregado_id bigint null,
    payload jsonb not null default '{}'::jsonb,
    publicado boolean not null default false,
    publicado_at timestamptz null,
    erro text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null
);

create index if not exists idx_escola_tenant_codigo on sigov.escola (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_ano_letivo_tenant_ano on sigov.ano_letivo (tenant_id, entidade_id, ano) where is_deleted = false;
create index if not exists idx_turma_tenant_escola on sigov.turma (tenant_id, entidade_id, escola_id) where is_deleted = false;
create index if not exists idx_turma_tenant_status on sigov.turma (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_aluno_tenant_codigo on sigov.aluno (tenant_id, entidade_id, codigo_aluno) where is_deleted = false;
create index if not exists idx_aluno_tenant_pessoa on sigov.aluno (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_responsavel_aluno_tenant_aluno on sigov.responsavel_aluno (tenant_id, entidade_id, aluno_id) where is_deleted = false;
create index if not exists idx_matricula_tenant_aluno on sigov.matricula (tenant_id, entidade_id, aluno_id) where is_deleted = false;
create index if not exists idx_matricula_tenant_turma on sigov.matricula (tenant_id, entidade_id, turma_id) where is_deleted = false;
create index if not exists idx_matricula_tenant_status on sigov.matricula (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_professor_tenant_pessoa on sigov.professor (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_professor_turma_tenant_turma on sigov.professor_turma (tenant_id, entidade_id, turma_id) where is_deleted = false;
create index if not exists idx_frequencia_tenant_turma_data on sigov.diario_frequencia (tenant_id, entidade_id, turma_id, data_aula) where is_deleted = false;
create unique index if not exists ux_frequencia_turma_aluno_data_componente on sigov.diario_frequencia (tenant_id, entidade_id, turma_id, aluno_id, data_aula, coalesce(componente_curricular, '')) where is_deleted = false;
create index if not exists idx_avaliacao_tenant_turma on sigov.avaliacao (tenant_id, entidade_id, turma_id) where is_deleted = false;
create index if not exists idx_nota_tenant_avaliacao on sigov.nota (tenant_id, entidade_id, avaliacao_id) where is_deleted = false;
create index if not exists idx_pre_matricula_tenant_protocolo on sigov.pre_matricula_inscricao (tenant_id, entidade_id, protocolo) where is_deleted = false;
create index if not exists idx_educacenso_tenant_status on sigov.educacenso_registro (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_educacao_evento_outbox on sigov.educacao_evento (tenant_id, publicado) where is_deleted = false;

create or replace view sigov.vw_educacao_dashboard as
select e.tenant_id, e.entidade_id,
       count(distinct e.id) filter (where e.is_deleted = false) as total_escolas,
       count(distinct a.id) filter (where a.situacao = 'ATIVO' and a.is_deleted = false) as total_alunos_ativos,
       count(distinct m.id) filter (where m.status = 'ATIVA' and m.is_deleted = false) as total_matriculas_ativas,
       count(distinct t.id) filter (where t.status = 'ABERTA' and t.is_deleted = false) as total_turmas_abertas,
       coalesce(sum(distinct t.capacidade),0)::bigint as vagas_totais,
       coalesce(sum(distinct t.vagas_ocupadas),0)::bigint as vagas_ocupadas
from sigov.escola e
left join sigov.aluno a on a.tenant_id = e.tenant_id and a.entidade_id = e.entidade_id
left join sigov.matricula m on m.tenant_id = e.tenant_id and m.entidade_id = e.entidade_id
left join sigov.turma t on t.tenant_id = e.tenant_id and t.entidade_id = e.entidade_id
group by e.tenant_id, e.entidade_id;

create or replace view sigov.vw_educacao_matriculas_por_escola as
select m.tenant_id, m.entidade_id, m.escola_id, e.nome as escola, count(*) as total
from sigov.matricula m join sigov.escola e on e.id = m.escola_id
where m.is_deleted = false
group by m.tenant_id, m.entidade_id, m.escola_id, e.nome;

create or replace view sigov.vw_educacao_frequencia_resumo as
select tenant_id, entidade_id, turma_id, date_trunc('month', data_aula)::date as mes,
       count(*) as aulas_registradas,
       avg(case when presente then 100.0 else 0.0 end)::numeric(9,2) as frequencia_media
from sigov.diario_frequencia
where is_deleted = false
group by tenant_id, entidade_id, turma_id, date_trunc('month', data_aula);

create or replace view sigov.vw_educacao_notas_resumo as
select n.tenant_id, n.entidade_id, av.turma_id, n.aluno_id, avg(n.valor)::numeric(9,2) as media_notas
from sigov.nota n join sigov.avaliacao av on av.id = n.avaliacao_id
where n.is_deleted = false
group by n.tenant_id, n.entidade_id, av.turma_id, n.aluno_id;

create or replace view sigov.vw_educacao_pre_matricula_resumo as
select tenant_id, entidade_id, ano_letivo, status, count(*) as total
from sigov.pre_matricula_inscricao
where is_deleted = false
group by tenant_id, entidade_id, ano_letivo, status;

do $$
declare
    t text;
begin
    foreach t in array array['escola','ano_letivo','curso','serie_ano','turma','aluno','responsavel_aluno','matricula','professor','professor_turma','diario_frequencia','avaliacao','nota','pre_matricula_inscricao','educacenso_registro','portal_educacao_acesso','educacao_evento'] loop
        execute format('drop trigger if exists trg_%s_touch on sigov.%I', t, t);
        execute format('create trigger trg_%s_touch before update on sigov.%I for each row execute function sigov.educacao_touch_updated_at()', t, t);
        execute format('alter table sigov.%I enable row level security', t);
        execute format('drop policy if exists %I on sigov.%I', 'rls_' || t || '_tenant_isolation', t);
        execute format('create policy %I on sigov.%I using (tenant_id = sigov.current_tenant_id()) with check (tenant_id = sigov.current_tenant_id())', 'rls_' || t || '_tenant_isolation', t);
    end loop;
end $$;

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo)
values ('educacao', 'Educação', 'Módulo base de gestão escolar do sigov', 'Operacional', 40, '/Educacao/Dashboard', 'book', true)
on conflict (codigo) do nothing;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
('educacao','escola','visualizar','educacao.escola.visualizar','Visualizar escolas',true),
('educacao','escola','criar','educacao.escola.criar','Criar escolas',true),
('educacao','escola','editar','educacao.escola.editar','Editar escolas',true),
('educacao','escola','excluir','educacao.escola.excluir','Excluir escolas',true),
('educacao','aluno','visualizar','educacao.aluno.visualizar','Visualizar alunos',true),
('educacao','aluno','criar','educacao.aluno.criar','Criar alunos',true),
('educacao','aluno','editar','educacao.aluno.editar','Editar alunos',true),
('educacao','aluno','excluir','educacao.aluno.excluir','Excluir alunos',true),
('educacao','aluno','visualizar_dados_completos','educacao.aluno.visualizar_dados_completos','Visualizar dados completos de aluno',true),
('educacao','matricula','visualizar','educacao.matricula.visualizar','Visualizar matrículas',true),
('educacao','matricula','criar','educacao.matricula.criar','Criar matrícula',true),
('educacao','matricula','cancelar','educacao.matricula.cancelar','Cancelar matrícula',true),
('educacao','matricula','transferir','educacao.matricula.transferir','Transferir matrícula',true),
('educacao','turma','visualizar','educacao.turma.visualizar','Visualizar turmas',true),
('educacao','turma','criar','educacao.turma.criar','Criar turmas',true),
('educacao','turma','editar','educacao.turma.editar','Editar turmas',true),
('educacao','turma','excluir','educacao.turma.excluir','Excluir turmas',true),
('educacao','professor','visualizar','educacao.professor.visualizar','Visualizar professores',true),
('educacao','professor','criar','educacao.professor.criar','Criar professores',true),
('educacao','professor','editar','educacao.professor.editar','Editar professores',true),
('educacao','frequencia','visualizar','educacao.frequencia.visualizar','Visualizar frequência',true),
('educacao','frequencia','criar','educacao.frequencia.criar','Registrar frequência',true),
('educacao','avaliacao','visualizar','educacao.avaliacao.visualizar','Visualizar avaliações',true),
('educacao','avaliacao','criar','educacao.avaliacao.criar','Criar avaliação',true),
('educacao','nota','criar','educacao.nota.criar','Registrar nota',true),
('educacao','pre_matricula','visualizar','educacao.pre_matricula.visualizar','Visualizar pré-matrícula',true),
('educacao','pre_matricula','criar','educacao.pre_matricula.criar','Criar pré-matrícula',true),
('educacao','pre_matricula','converter','educacao.pre_matricula.converter','Converter pré-matrícula',true),
('educacao','educacenso','visualizar','educacao.educacenso.visualizar','Visualizar Educacenso estrutural',true),
('educacao','educacenso','registrar','educacao.educacenso.registrar','Registrar Educacenso estrutural',true),
('educacao','dashboard','visualizar','educacao.dashboard.visualizar','Visualizar dashboard educação',true),
('educacao','exportar','exportar','educacao.exportar','Exportar dados de educação',true)
on conflict do nothing;

-- Seeds de desenvolvimento seguros: só inserem se existir tenant e entidade base.
do $$
declare
    v_tenant bigint;
    v_entidade bigint;
    v_exercicio bigint;
    v_escola bigint;
    v_ano bigint;
    v_curso bigint;
    v_serie bigint;
begin
    select id into v_tenant from sigov.tenant where is_deleted = false order by id limit 1;
    select id into v_entidade from sigov.entidade where is_deleted = false order by id limit 1;
    select id into v_exercicio from sigov.exercicio where is_deleted = false order by ano desc limit 1;
    if v_tenant is not null and v_entidade is not null then
        insert into sigov.escola (tenant_id, entidade_id, codigo, nome, tipo_escola, situacao)
        values (v_tenant, v_entidade, 'ESC-DEMO', 'Escola Municipal Demo', 'MUNICIPAL', 'ATIVA')
        on conflict (tenant_id, entidade_id, codigo) do nothing;
        select id into v_escola from sigov.escola where tenant_id = v_tenant and entidade_id = v_entidade and codigo = 'ESC-DEMO' limit 1;
        insert into sigov.ano_letivo (tenant_id, entidade_id, exercicio_id, escola_id, ano, data_inicio, data_fim, status)
        values (v_tenant, v_entidade, v_exercicio, v_escola, extract(year from current_date)::int, make_date(extract(year from current_date)::int,1,1), make_date(extract(year from current_date)::int,12,31), 'ABERTO')
        on conflict do nothing;
        select id into v_ano from sigov.ano_letivo where tenant_id = v_tenant and entidade_id = v_entidade and ano = extract(year from current_date)::int limit 1;
        insert into sigov.curso (tenant_id, entidade_id, codigo, nome, etapa_ensino)
        values (v_tenant, v_entidade, 'EF', 'Ensino Fundamental', 'ENSINO_FUNDAMENTAL')
        on conflict (tenant_id, entidade_id, codigo) do nothing;
        select id into v_curso from sigov.curso where tenant_id = v_tenant and entidade_id = v_entidade and codigo = 'EF' limit 1;
        insert into sigov.serie_ano (tenant_id, entidade_id, curso_id, codigo, nome, ordem)
        values (v_tenant, v_entidade, v_curso, '1ANO', '1º Ano', 1)
        on conflict (tenant_id, entidade_id, curso_id, codigo) do nothing;
        select id into v_serie from sigov.serie_ano where tenant_id = v_tenant and entidade_id = v_entidade and curso_id = v_curso and codigo = '1ANO' limit 1;
        if v_escola is not null and v_ano is not null and v_curso is not null and v_serie is not null then
            insert into sigov.turma (tenant_id, entidade_id, exercicio_id, escola_id, ano_letivo_id, curso_id, serie_ano_id, codigo, nome, turno, capacidade, status)
            values (v_tenant, v_entidade, v_exercicio, v_escola, v_ano, v_curso, v_serie, '1A', '1º Ano A', 'MATUTINO', 30, 'ABERTA')
            on conflict (tenant_id, entidade_id, ano_letivo_id, codigo) do nothing;
        end if;
    end if;
end $$;
