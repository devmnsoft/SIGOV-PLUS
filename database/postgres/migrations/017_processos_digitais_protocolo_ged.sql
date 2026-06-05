-- Módulo Processos Digitais, Protocolo, GED básico, Ouvidoria e Diário Oficial.
-- Idempotente e restrito ao schema sigov.


create table if not exists sigov.tipo_processo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(150) not null,
    descricao text null,
    prazo_padrao_dias integer null,
    exige_interessado boolean not null default true,
    permite_sigilo boolean not null default true,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, nome)
);

create table if not exists sigov.processo_digital (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    tipo_processo_id bigint not null references sigov.tipo_processo(id),
    numero varchar(50) not null,
    ano integer not null,
    assunto varchar(250) not null,
    descricao text null,
    interessado_pessoa_id bigint null references sigov.pessoa(id),
    unidade_origem_id bigint null references sigov.unidade_organizacional(id),
    unidade_atual_id bigint null references sigov.unidade_organizacional(id),
    usuario_abertura_id bigint not null references sigov.usuario(id),
    status varchar(40) not null,
    prioridade varchar(30) not null,
    sigiloso boolean not null default false,
    data_abertura timestamptz not null default now(),
    data_encerramento timestamptz null,
    prazo_resposta_at timestamptz null,
    metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_processo_status check (status in ('ABERTO','EM_TRAMITACAO','AGUARDANDO_DOCUMENTO','AGUARDANDO_ASSINATURA','SUSPENSO','ENCERRADO','CANCELADO')),
    constraint ck_processo_prioridade check (prioridade in ('BAIXA','NORMAL','ALTA','URGENTE')),
    unique (tenant_id, entidade_id, exercicio_id, ano, numero)
);

create table if not exists sigov.processo_movimentacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    processo_digital_id bigint not null references sigov.processo_digital(id),
    unidade_origem_id bigint null references sigov.unidade_organizacional(id),
    unidade_destino_id bigint null references sigov.unidade_organizacional(id),
    usuario_origem_id bigint not null references sigov.usuario(id),
    usuario_destino_id bigint null references sigov.usuario(id),
    despacho text not null,
    status_anterior varchar(40) null,
    status_novo varchar(40) null,
    movimentado_at timestamptz not null default now(),
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

create table if not exists sigov.processo_responsavel (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    processo_digital_id bigint not null references sigov.processo_digital(id),
    usuario_id bigint not null references sigov.usuario(id),
    papel varchar(80) not null,
    principal boolean not null default false,
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

create table if not exists sigov.processo_parecer (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    processo_digital_id bigint not null references sigov.processo_digital(id),
    usuario_id bigint not null references sigov.usuario(id),
    titulo varchar(150) not null,
    texto text not null,
    tipo_parecer varchar(50) not null,
    parecer_at timestamptz not null default now(),
    sigiloso boolean not null default false,
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

create table if not exists sigov.processo_anexo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    processo_digital_id bigint not null references sigov.processo_digital(id),
    arquivo_id bigint null references sigov.arquivo(id),
    nome_arquivo varchar(250) not null,
    content_type varchar(100) null,
    tamanho_bytes bigint null,
    hash_sha256 varchar(128) null,
    observacao text null,
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

create table if not exists sigov.modelo_documento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(150) not null,
    tipo varchar(80) not null,
    conteudo_template text not null,
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

create table if not exists sigov.documento_gerado (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    processo_digital_id bigint null references sigov.processo_digital(id),
    modelo_documento_id bigint null references sigov.modelo_documento(id),
    titulo varchar(250) not null,
    conteudo text not null,
    status varchar(40) not null default 'RASCUNHO',
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

create table if not exists sigov.assinatura_digital (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    documento_gerado_id bigint null references sigov.documento_gerado(id),
    processo_digital_id bigint null references sigov.processo_digital(id),
    usuario_id bigint not null references sigov.usuario(id),
    status varchar(40) not null,
    evidencia jsonb not null default '{}'::jsonb,
    solicitado_at timestamptz not null default now(),
    assinado_at timestamptz null,
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

create table if not exists sigov.fila_assinatura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    assinatura_digital_id bigint not null references sigov.assinatura_digital(id),
    usuario_id bigint not null references sigov.usuario(id),
    ordem integer not null default 1,
    status varchar(40) not null default 'PENDENTE',
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

create table if not exists sigov.protocolo_atendimento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    numero varchar(50) not null,
    pessoa_id bigint null references sigov.pessoa(id),
    assunto varchar(250) not null,
    descricao text null,
    canal varchar(50) not null,
    status varchar(40) not null,
    processo_digital_id bigint null references sigov.processo_digital(id),
    usuario_responsavel_id bigint null references sigov.usuario(id),
    aberto_at timestamptz not null default now(),
    encerrado_at timestamptz null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_protocolo_canal check (canal in ('PRESENCIAL','TELEFONE','EMAIL','PORTAL','WHATSAPP','OUTROS')),
    constraint ck_protocolo_status check (status in ('ABERTO','EM_ATENDIMENTO','CONVERTIDO_PROCESSO','ENCERRADO','CANCELADO')),
    unique (tenant_id, entidade_id, exercicio_id, numero)
);

create table if not exists sigov.ouvidoria_manifestacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    numero varchar(50) not null,
    pessoa_id bigint null references sigov.pessoa(id),
    tipo_manifestacao varchar(50) not null,
    assunto varchar(250) not null,
    descricao text not null,
    status varchar(40) not null,
    anonima boolean not null default false,
    sigilosa boolean not null default true,
    processo_digital_id bigint null references sigov.processo_digital(id),
    resposta text null,
    respondido_at timestamptz null,
    respondido_by bigint null references sigov.usuario(id),
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ouvidoria_tipo check (tipo_manifestacao in ('RECLAMACAO','DENUNCIA','ELOGIO','SUGESTAO','SOLICITACAO','INFORMACAO')),
    constraint ck_ouvidoria_status check (status in ('RECEBIDA','EM_ANALISE','ENCAMINHADA','RESPONDIDA','ARQUIVADA','CANCELADA')),
    unique (tenant_id, entidade_id, exercicio_id, numero)
);

create table if not exists sigov.diario_oficial_publicacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    numero_edicao varchar(50) not null,
    data_publicacao date not null,
    titulo varchar(250) not null,
    descricao text null,
    status varchar(40) not null,
    publicado_at timestamptz null,
    publicado_by bigint null references sigov.usuario(id),
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_diario_status check (status in ('RASCUNHO','AGENDADO','PUBLICADO','CANCELADO')),
    unique (tenant_id, entidade_id, exercicio_id, numero_edicao)
);

create table if not exists sigov.ato_oficial (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    diario_oficial_publicacao_id bigint not null references sigov.diario_oficial_publicacao(id),
    tipo_ato varchar(80) not null,
    numero varchar(80) null,
    titulo varchar(250) not null,
    texto text not null,
    data_ato date null,
    origem varchar(150) null,
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

create index if not exists idx_tipo_processo_tenant on sigov.tipo_processo (tenant_id, entidade_id, ativo) where is_deleted = false;
create index if not exists idx_processo_tenant_numero on sigov.processo_digital (tenant_id, entidade_id, exercicio_id, numero) where is_deleted = false;
create index if not exists idx_processo_tenant_status on sigov.processo_digital (tenant_id, status) where is_deleted = false;
create index if not exists idx_processo_tenant_interessado on sigov.processo_digital (tenant_id, interessado_pessoa_id) where is_deleted = false;
create index if not exists idx_processo_tenant_unidade_atual on sigov.processo_digital (tenant_id, unidade_atual_id) where is_deleted = false;
create index if not exists idx_processo_tenant_data_abertura on sigov.processo_digital (tenant_id, data_abertura desc) where is_deleted = false;
create index if not exists idx_processo_movimentacao_processo on sigov.processo_movimentacao (tenant_id, processo_digital_id, movimentado_at desc) where is_deleted = false;
create index if not exists idx_processo_parecer_processo on sigov.processo_parecer (tenant_id, processo_digital_id, parecer_at desc) where is_deleted = false;
create index if not exists idx_protocolo_tenant_numero on sigov.protocolo_atendimento (tenant_id, entidade_id, exercicio_id, numero) where is_deleted = false;
create index if not exists idx_protocolo_tenant_status on sigov.protocolo_atendimento (tenant_id, status) where is_deleted = false;
create index if not exists idx_ouvidoria_tenant_numero on sigov.ouvidoria_manifestacao (tenant_id, entidade_id, exercicio_id, numero) where is_deleted = false;
create index if not exists idx_ouvidoria_tenant_status on sigov.ouvidoria_manifestacao (tenant_id, status) where is_deleted = false;
create index if not exists idx_diario_tenant_data_publicacao on sigov.diario_oficial_publicacao (tenant_id, data_publicacao desc) where is_deleted = false;
create index if not exists idx_ato_diario on sigov.ato_oficial (tenant_id, diario_oficial_publicacao_id) where is_deleted = false;

insert into sigov.tipo_processo (tenant_id, entidade_id, nome, descricao, prazo_padrao_dias, exige_interessado, permite_sigilo)
select t.id, null, v.nome, v.descricao, v.prazo, true, true
from sigov.tenant t
cross join (values
    ('Administrativo', 'Processo administrativo geral', 30),
    ('Licitação', 'Processos de contratação pública', 60),
    ('Requerimento', 'Requerimentos de cidadãos e empresas', 20),
    ('Ouvidoria', 'Demandas convertidas da ouvidoria', 30),
    ('Protocolo Geral', 'Demandas convertidas do protocolo geral', 15)
) as v(nome, descricao, prazo)
where t.is_deleted = false
  and not exists (
      select 1 from sigov.tipo_processo tp
      where tp.tenant_id = t.id
        and tp.entidade_id is null
        and tp.nome = v.nome
        and tp.is_deleted = false
  );

insert into sigov.permissao (modulo, chave, recurso, acao, descricao, ativo)
select 'processos', p.chave, split_part(p.chave, '.', 1) || '.' || split_part(p.chave, '.', 2), split_part(p.chave, '.', 3), p.descricao, true
from (values
 ('processos.tipo.visualizar','Visualizar tipos de processo'),
 ('processos.tipo.criar','Criar tipos de processo'),
 ('processos.tipo.editar','Editar tipos de processo'),
 ('processos.tipo.excluir','Excluir tipos de processo'),
 ('processos.processo.visualizar','Visualizar processos digitais'),
 ('processos.processo.criar','Criar processos digitais'),
 ('processos.processo.editar','Editar processos digitais'),
 ('processos.processo.excluir','Excluir processos digitais'),
 ('processos.processo.movimentar','Movimentar processos digitais'),
 ('processos.processo.parecer','Emitir pareceres'),
 ('processos.processo.encerrar','Encerrar processos digitais'),
 ('processos.processo.cancelar','Cancelar processos digitais'),
 ('processos.processo.visualizar_sigiloso','Visualizar processos sigilosos'),
 ('processos.protocolo.visualizar','Visualizar protocolos'),
 ('processos.protocolo.criar','Criar protocolos'),
 ('processos.protocolo.editar','Editar protocolos'),
 ('processos.protocolo.encerrar','Encerrar protocolos'),
 ('processos.protocolo.converter','Converter protocolos em processo'),
 ('processos.ouvidoria.visualizar','Visualizar ouvidoria'),
 ('processos.ouvidoria.criar','Criar manifestações de ouvidoria'),
 ('processos.ouvidoria.responder','Responder manifestações de ouvidoria'),
 ('processos.ouvidoria.converter','Converter ouvidoria em processo'),
 ('processos.ouvidoria.arquivar','Arquivar ouvidoria'),
 ('processos.diario.visualizar','Visualizar diário oficial'),
 ('processos.diario.criar','Criar publicações do diário oficial'),
 ('processos.diario.editar','Editar publicações do diário oficial'),
 ('processos.diario.publicar','Publicar diário oficial')
) as p(chave, descricao)
on conflict (modulo, chave) do update set recurso = excluded.recurso, acao = excluded.acao, descricao = excluded.descricao, ativo = true, is_deleted = false;
