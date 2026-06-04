create table if not exists sigov.usuario (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    pessoa_id bigint null references sigov.pessoa(id),
    login varchar(100) not null,
    email varchar(250) not null,
    senha_hash text not null,
    mfa_habilitado boolean not null default false,
    ultimo_login_at timestamptz null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.grupo_acesso (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(150) not null,
    descricao text null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.usuario_grupo (
    usuario_id bigint not null references sigov.usuario(id),
    grupo_acesso_id bigint not null references sigov.grupo_acesso(id),
    created_at timestamptz not null default now(),
    created_by bigint null,
    correlation_id uuid null,
    primary key (usuario_id, grupo_acesso_id)
);

create table if not exists sigov.perfil_acesso (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(150) not null,
    descricao text null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.permissao (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    modulo varchar(60) not null,
    chave varchar(150) not null,
    descricao text null,
    codigo_externo varchar(100) null,
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
    unique (modulo, chave)
);

create table if not exists sigov.perfil_permissao (
    perfil_acesso_id bigint not null references sigov.perfil_acesso(id),
    permissao_id bigint not null references sigov.permissao(id),
    created_at timestamptz not null default now(),
    created_by bigint null,
    correlation_id uuid null,
    primary key (perfil_acesso_id, permissao_id)
);

create table if not exists sigov.sessao_usuario (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    usuario_id bigint not null references sigov.usuario(id),
    token_hash text not null,
    ip varchar(60) null,
    user_agent text null,
    expira_at timestamptz not null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.token_mfa (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    usuario_id bigint not null references sigov.usuario(id),
    token_hash text not null,
    expira_at timestamptz not null,
    utilizado_at timestamptz null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.politica_senha (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    tamanho_minimo integer not null default 10,
    exigir_mfa boolean not null default false,
    validade_dias integer null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.historico_login (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    usuario_id bigint null references sigov.usuario(id),
    login varchar(100) not null,
    sucesso boolean not null,
    ip varchar(60) null,
    user_agent text null,
    motivo_falha text null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.api_credential (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(150) not null,
    api_key_hash text not null,
    escopos jsonb null,
    expira_at timestamptz null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.trilha_auditoria (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    usuario_id bigint null references sigov.usuario(id),
    tabela varchar(120) not null,
    registro_id varchar(80) null,
    acao varchar(60) not null,
    valores_anteriores jsonb null,
    valores_novos jsonb null,
    ip varchar(60) null,
    user_agent text null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.acesso_dado_pessoal (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    usuario_id bigint null references sigov.usuario(id),
    finalidade varchar(250) not null,
    campos_acessados jsonb null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.consentimento (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    finalidade varchar(250) not null,
    concedido boolean not null,
    concedido_at timestamptz not null default now(),
    revogado_at timestamptz null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.solicitacao_titular (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    tipo varchar(60) not null,
    status varchar(60) not null default 'ABERTA',
    descricao text null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.relatorio_titular (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    solicitacao_titular_id bigint not null references sigov.solicitacao_titular(id),
    conteudo jsonb not null,
    gerado_at timestamptz not null default now(),
    codigo_externo varchar(100) null,
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

create table if not exists sigov.processo_tratamento (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(180) not null,
    base_legal varchar(120) not null,
    dados_tratados jsonb null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.incidente_seguranca (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    titulo varchar(180) not null,
    descricao text not null,
    severidade varchar(30) not null,
    status varchar(30) not null default 'ABERTO',
    codigo_externo varchar(100) null,
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

create table if not exists sigov.retencao_descarte (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    tabela varchar(120) not null,
    prazo_dias integer not null,
    acao varchar(60) not null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.anonimizacao (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    status varchar(60) not null default 'PENDENTE',
    executada_at timestamptz null,
    codigo_externo varchar(100) null,
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

create table if not exists sigov.dpo_historico (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    nome varchar(180) not null,
    email varchar(250) not null,
    inicio_at timestamptz not null,
    fim_at timestamptz null,
    codigo_externo varchar(100) null,
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
