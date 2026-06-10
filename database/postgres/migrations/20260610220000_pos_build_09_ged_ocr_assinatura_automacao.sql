-- SIGOV PLUS - Pós-Build 09: GED/OCR, assinatura digital simulada e automação documental.
-- Idempotente, multi-tenant e restrito ao schema sigov.

create table if not exists sigov.ged_tipo_documento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    codigo varchar(60) not null,
    nome varchar(150) not null,
    descricao text null,
    exige_assinatura boolean not null default false,
    permite_ocr boolean not null default true,
    prazo_retencao_dias integer null,
    metadados_obrigatorios jsonb not null default '[]'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, codigo)
);

create table if not exists sigov.ged_template_documento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    codigo varchar(80) not null,
    nome varchar(150) not null,
    tipo_documento_codigo varchar(60) not null,
    conteudo_template text not null,
    variaveis jsonb not null default '[]'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, codigo)
);

create table if not exists sigov.contrato (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    numero varchar(80) not null,
    objeto varchar(300) not null,
    contratado_nome varchar(200) not null,
    contratado_documento varchar(30) null,
    origem_modulo varchar(60) null,
    origem_id bigint null,
    valor_total numeric(18,2) not null default 0,
    data_inicio date null,
    data_fim date null,
    status varchar(40) not null default 'RASCUNHO',
    metadados jsonb not null default '{}'::jsonb,
    lgpd_classificacao varchar(40) not null default 'DADO_CONTROLADO',
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_contrato_status check (status in ('RASCUNHO','EM_ANALISE','AGUARDANDO_ASSINATURA','ASSINADO','VIGENTE','SUSPENSO','ENCERRADO','CANCELADO')),
    unique (tenant_id, entidade_id, numero)
);

create table if not exists sigov.protocolo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    numero varchar(80) not null,
    assunto varchar(250) not null,
    interessado_nome varchar(200) null,
    interessado_documento varchar(30) null,
    canal varchar(50) not null default 'PORTAL',
    status varchar(40) not null default 'ABERTO',
    documento_id bigint null,
    contrato_id bigint null references sigov.contrato(id),
    metadados jsonb not null default '{}'::jsonb,
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
    constraint ck_protocolo_pos09_canal check (canal in ('PRESENCIAL','TELEFONE','EMAIL','PORTAL','WHATSAPP','INTEGRACAO','OUTROS')),
    constraint ck_protocolo_pos09_status check (status in ('ABERTO','EM_TRAMITACAO','AGUARDANDO_DOCUMENTO','AGUARDANDO_ASSINATURA','ENCERRADO','CANCELADO')),
    unique (tenant_id, entidade_id, numero)
);

create table if not exists sigov.ged_documento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    tipo_documento_id bigint null references sigov.ged_tipo_documento(id),
    protocolo_id bigint null references sigov.protocolo(id),
    contrato_id bigint null references sigov.contrato(id),
    origem_modulo varchar(60) null,
    origem_entidade varchar(100) null,
    origem_id bigint null,
    titulo varchar(250) not null,
    descricao text null,
    numero_documento varchar(100) null,
    tipo varchar(80) not null default 'GERAL',
    status varchar(40) not null default 'RASCUNHO',
    classificacao_lgpd varchar(40) not null default 'DADO_CONTROLADO',
    sigiloso boolean not null default false,
    metadados jsonb not null default '{}'::jsonb,
    tags text[] not null default '{}'::text[],
    data_documento date null,
    publicado_at timestamptz null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ged_documento_status check (status in ('RASCUNHO','RECEBIDO','INDEXADO','EM_WORKFLOW','AGUARDANDO_ASSINATURA','ASSINADO','PUBLICADO','ARQUIVADO','CANCELADO'))
);

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'fk_protocolo_pos09_documento') then
        alter table sigov.protocolo add constraint fk_protocolo_pos09_documento foreign key (documento_id) references sigov.ged_documento(id) not valid;
    end if;
end $$;

create table if not exists sigov.ged_anexo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint not null references sigov.ged_documento(id),
    arquivo_id bigint null references sigov.arquivo(id),
    nome_arquivo varchar(250) not null,
    content_type varchar(120) null,
    tamanho_bytes bigint null,
    hash_sha256 varchar(128) null,
    storage_key varchar(500) null,
    versao integer not null default 1,
    principal boolean not null default false,
    texto_extraido text null,
    ocr_status varchar(40) not null default 'PENDENTE',
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ged_anexo_ocr_status check (ocr_status in ('PENDENTE','PROCESSANDO','PROCESSADO','FALHA','NAO_APLICAVEL'))
);

create table if not exists sigov.ged_indice (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint not null references sigov.ged_documento(id),
    chave varchar(120) not null,
    valor text not null,
    tipo_valor varchar(40) not null default 'TEXTO',
    origem varchar(40) not null default 'MANUAL',
    confianca numeric(5,2) null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ged_indice_origem check (origem in ('MANUAL','OCR','INTEGRACAO','WORKFLOW'))
);

create table if not exists sigov.ocr_digitalizacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint not null references sigov.ged_documento(id),
    anexo_id bigint null references sigov.ged_anexo(id),
    status varchar(40) not null default 'PENDENTE',
    motor varchar(80) not null default 'SIMULADO',
    idioma varchar(20) not null default 'pt-BR',
    texto_extraido text null,
    metadados_extraidos jsonb not null default '{}'::jsonb,
    confianca_media numeric(5,2) null,
    iniciado_at timestamptz null,
    concluido_at timestamptz null,
    erro text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ocr_status check (status in ('PENDENTE','PROCESSANDO','PROCESSADO','FALHA','CANCELADO'))
);

create table if not exists sigov.ged_assinatura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint not null references sigov.ged_documento(id),
    contrato_id bigint null references sigov.contrato(id),
    usuario_id bigint null references sigov.usuario(id),
    signatario_nome varchar(200) not null,
    signatario_documento varchar(30) null,
    tipo varchar(40) not null default 'SIMULADA',
    status varchar(40) not null default 'PENDENTE',
    hash_assinatura varchar(128) null,
    evidencias jsonb not null default '{}'::jsonb,
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
    correlation_id uuid null,
    constraint ck_ged_assinatura_status check (status in ('PENDENTE','ASSINADO','RECUSADO','EXPIRADO','CANCELADO')),
    constraint ck_ged_assinatura_tipo check (tipo in ('SIMULADA','ICP_BRASIL_FUTURA','GOVBR_FUTURA'))
);

create table if not exists sigov.ged_workflow (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint null references sigov.ged_documento(id),
    codigo varchar(80) not null,
    nome varchar(150) not null,
    etapa_atual varchar(120) not null default 'INICIO',
    status varchar(40) not null default 'ATIVO',
    responsavel_usuario_id bigint null references sigov.usuario(id),
    responsavel_perfil varchar(100) null,
    definicao jsonb not null default '{}'::jsonb,
    iniciado_at timestamptz not null default now(),
    concluido_at timestamptz null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    constraint ck_ged_workflow_status check (status in ('ATIVO','PAUSADO','CONCLUIDO','CANCELADO'))
);

create table if not exists sigov.fluxo_tramitacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint null references sigov.ged_documento(id),
    protocolo_id bigint null references sigov.protocolo(id),
    workflow_id bigint null references sigov.ged_workflow(id),
    unidade_origem_id bigint null references sigov.unidade_organizacional(id),
    unidade_destino_id bigint null references sigov.unidade_organizacional(id),
    usuario_origem_id bigint null references sigov.usuario(id),
    usuario_destino_id bigint null references sigov.usuario(id),
    despacho text not null,
    status_anterior varchar(40) null,
    status_novo varchar(40) not null default 'EM_TRAMITACAO',
    prazo_at timestamptz null,
    tramitado_at timestamptz not null default now(),
    recebido_at timestamptz null,
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

create table if not exists sigov.ged_historico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    documento_id bigint null references sigov.ged_documento(id),
    protocolo_id bigint null references sigov.protocolo(id),
    contrato_id bigint null references sigov.contrato(id),
    acao varchar(100) not null,
    descricao text not null,
    usuario_id bigint null references sigov.usuario(id),
    antes jsonb null,
    depois jsonb null,
    ip varchar(60) null,
    user_agent text null,
    evento_at timestamptz not null default now(),
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

create index if not exists idx_ged_documento_tenant_status on sigov.ged_documento (tenant_id, status, created_at desc);
create index if not exists idx_ged_documento_tenant_tipo on sigov.ged_documento (tenant_id, tipo, data_documento desc);
create index if not exists idx_ged_documento_metadata on sigov.ged_documento using gin (metadados);
create index if not exists idx_ged_documento_tags on sigov.ged_documento using gin (tags);
create index if not exists idx_ged_anexo_tenant_documento on sigov.ged_anexo (tenant_id, documento_id, versao desc);
create index if not exists idx_ged_indice_tenant_chave_valor on sigov.ged_indice (tenant_id, chave, valor);
create index if not exists idx_ged_historico_tenant_documento on sigov.ged_historico (tenant_id, documento_id, evento_at desc);
create index if not exists idx_ged_assinatura_tenant_status on sigov.ged_assinatura (tenant_id, status, solicitado_at desc);
create index if not exists idx_ged_workflow_tenant_status on sigov.ged_workflow (tenant_id, status, etapa_atual);
create index if not exists idx_protocolo_tenant_status_data on sigov.protocolo (tenant_id, status, aberto_at desc);
create index if not exists idx_contrato_tenant_status_data on sigov.contrato (tenant_id, status, data_inicio desc);
create index if not exists idx_fluxo_tramitacao_tenant_doc on sigov.fluxo_tramitacao (tenant_id, documento_id, tramitado_at desc);
create index if not exists idx_ocr_tenant_status on sigov.ocr_digitalizacao (tenant_id, status, created_at desc);

insert into sigov.permissao (modulo,recurso,acao,chave,descricao,ativo) values
('ged','documento','visualizar','ged.visualizar','Visualizar GED, documentos e dashboard documental.',true),
('ged','documento','upload','ged.upload','Enviar documentos e anexos ao GED.',true),
('ged','documento','download','ged.download','Baixar documentos e anexos do GED.',true),
('ged','indice','indexar','ged.indexar','Indexar metadados e resultados de OCR.',true),
('ged','assinatura','assinar','ged.assinar','Executar assinatura digital simulada.',true),
('ged','tramitacao','tramitar','ged.tramitar','Tramitar documentos e protocolos eletrônicos.',true),
('contrato','contrato','visualizar','contrato.visualizar','Visualizar contratos jurídicos e comerciais.',true),
('contrato','contrato','criar','contrato.criar','Criar contratos vinculados ao GED.',true),
('contrato','assinatura','assinar','contrato.assinar','Assinar contratos de forma simulada.',true),
('fluxo','workflow','visualizar','fluxo.visualizar','Visualizar fluxos e workflows documentais.',true),
('ocr','digitalizacao','processar','ocr.processar','Processar OCR simulado de documentos.',true)
on conflict (modulo,recurso,acao) do update set chave=excluded.chave, descricao=excluded.descricao, ativo=true;

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
select pa.id, p.id
from sigov.perfil_acesso pa
join sigov.permissao p on p.chave in ('ged.visualizar','ged.upload','ged.download','ged.indexar','ged.assinar','ged.tramitar','contrato.visualizar','contrato.criar','contrato.assinar','fluxo.visualizar','ocr.processar')
where pa.ativo=true and pa.is_deleted=false
  and (coalesce(pa.codigo_externo, upper(replace(pa.nome,' ','_'))) in ('ADMIN_GERAL','ADMINISTRADOR_GERAL','ADMIN_TENANT','ADMINISTRADOR_TENANT') or upper(pa.nome) like '%ADMIN%')
on conflict do nothing;

insert into sigov.ged_tipo_documento (tenant_id,codigo,nome,descricao,exige_assinatura,permite_ocr,metadados_obrigatorios)
select t.id, seed.codigo, seed.nome, seed.descricao, seed.exige_assinatura, seed.permite_ocr, seed.metadados::jsonb
from sigov.tenant t
cross join (values
    ('CONTRATO','Contrato','Instrumentos jurídicos, comerciais e administrativos.',true,true,'["numero","vigencia","contratado"]'),
    ('DOCUMENTO_FISCAL','Documento fiscal','Notas, DAM, comprovantes e livros fiscais vinculados ao Tributário.',false,true,'["competencia","valor","origem"]'),
    ('CHECKLIST_PRODUCAO','Checklist de produção','Checklist, qualidade e evidências de ordens industriais.',false,true,'["ordem","produto","etapa"]'),
    ('PROTOCOLO','Protocolo eletrônico','Documentos recebidos via protocolo e atendimento.',false,true,'["numero_protocolo","canal"]')
) as seed(codigo,nome,descricao,exige_assinatura,permite_ocr,metadados)
where t.slug in ('plataforma-global','prefeitura-demo','tenant-demo')
on conflict (tenant_id,codigo) do update set nome=excluded.nome, descricao=excluded.descricao, exige_assinatura=excluded.exige_assinatura, permite_ocr=excluded.permite_ocr, metadados_obrigatorios=excluded.metadados_obrigatorios, updated_at=now();

insert into sigov.ged_template_documento (tenant_id,codigo,nome,tipo_documento_codigo,conteudo_template,variaveis)
select t.id, seed.codigo, seed.nome, seed.tipo, seed.template, seed.variaveis::jsonb
from sigov.tenant t
cross join (values
    ('TPL_CONTRATO_PADRAO','Contrato padrão','CONTRATO','Contrato nº {{numero}} firmado com {{contratado}} no valor de {{valor_total}}.','["numero","contratado","valor_total"]'),
    ('TPL_TERMO_ACEITE','Termo de aceite documental','PROTOCOLO','Termo de aceite do protocolo {{numero_protocolo}} recebido em {{data}}.','["numero_protocolo","data"]'),
    ('TPL_CHECKLIST_OP','Checklist de ordem de produção','CHECKLIST_PRODUCAO','Checklist da ordem {{ordem}} para produto {{produto}} na etapa {{etapa}}.','["ordem","produto","etapa"]')
) as seed(codigo,nome,tipo,template,variaveis)
where t.slug in ('plataforma-global','prefeitura-demo','tenant-demo')
on conflict (tenant_id,codigo) do update set nome=excluded.nome, tipo_documento_codigo=excluded.tipo_documento_codigo, conteudo_template=excluded.conteudo_template, variaveis=excluded.variaveis, updated_at=now();

insert into sigov.ged_workflow (tenant_id,codigo,nome,etapa_atual,status,definicao)
select t.id, 'WF_GED_BASICO', 'Workflow básico GED/OCR/Assinatura', 'RECEBIMENTO', 'ATIVO', '{"etapas":["RECEBIMENTO","OCR","INDEXACAO","TRAMITACAO","ASSINATURA","ARQUIVAMENTO"],"assinatura":"SIMULADA"}'::jsonb
from sigov.tenant t
where t.slug in ('plataforma-global','prefeitura-demo','tenant-demo')
  and not exists (select 1 from sigov.ged_workflow w where w.tenant_id=t.id and w.codigo='WF_GED_BASICO' and w.documento_id is null);

insert into sigov.tenant_modulo_pacote (codigo, nome, descricao, modulos_json) values
('GED_AUTOMACAO_PLUS','GED Automação Plus','GED completo com OCR simulado, contratos, protocolos, workflow, tramitação e assinatura digital simulada.', '["ged","ocr","contrato","fluxo","processos","integracoes","auditoria-lgpd"]'::jsonb)
on conflict (codigo) do update set nome=excluded.nome, descricao=excluded.descricao, modulos_json=excluded.modulos_json;
