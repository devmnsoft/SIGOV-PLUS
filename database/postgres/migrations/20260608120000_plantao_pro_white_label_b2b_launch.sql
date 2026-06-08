create schema if not exists sigov;
create schema if not exists plantaopro;

create table if not exists sigov.b2b_planos (
    id bigint generated always as identity primary key,
    codigo varchar(80) not null unique,
    nome varchar(160) not null,
    descricao text null,
    publico_alvo varchar(160) null,
    valor_mensal numeric(18,2) not null default 0,
    permite_white_label boolean not null default false,
    permite_api boolean not null default false,
    limite_usuarios bigint not null default 0,
    limite_medicos bigint not null default 0,
    limite_hospitais bigint not null default 0,
    limite_plantoes_mes bigint not null default 0,
    sla_resumo varchar(250) null,
    ordem integer not null default 0,
    ativo boolean not null default true,
    publico boolean not null default true,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_assinaturas (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    plano_id bigint not null references sigov.b2b_planos(id),
    status varchar(40) not null,
    inicio_vigencia timestamptz not null default now(),
    fim_vigencia timestamptz null,
    created_at timestamptz not null default now(),
    created_by bigint null
);

create table if not exists sigov.b2b_tenant_white_label (
    tenant_id bigint primary key references sigov.tenant(id),
    nome_plataforma varchar(160) not null default 'PlantãoPro',
    nome_comercial varchar(160) not null default 'PlantãoPro',
    logo_principal_url text null,
    logo_reduzida_url text null,
    favicon_url text null,
    banner_login_url text null,
    imagem_institucional_url text null,
    cor_primaria varchar(20) not null default '#2563eb',
    cor_secundaria varchar(20) not null default '#0f172a',
    cor_destaque varchar(20) not null default '#22c55e',
    cor_menu varchar(20) not null default '#111827',
    cor_fundo varchar(20) not null default '#f8fafc',
    tema varchar(20) not null default 'claro',
    slogan varchar(250) null,
    texto_boas_vindas text null,
    texto_rodape text null,
    dominio_customizado varchar(250) null,
    subdominio varchar(120) null,
    email_remetente varchar(250) null,
    termos_customizados text null,
    politica_privacidade_customizada text null,
    mobile_config jsonb not null default '{}'::jsonb,
    publicado boolean not null default false,
    publicado_at timestamptz null,
    updated_at timestamptz null,
    updated_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_white_label_publicacoes (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    status varchar(40) not null,
    diff_json jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    created_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_cadastro_cliente_solicitacoes (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    razao_social varchar(250) not null,
    nome_fantasia varchar(250) null,
    cnpj varchar(30) not null,
    responsavel_nome varchar(180) not null,
    responsavel_email varchar(250) not null,
    responsavel_telefone varchar(40) null,
    plano_id bigint not null references sigov.b2b_planos(id),
    status varchar(60) not null,
    ip_origem varchar(80) null,
    user_agent varchar(250) null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_cadastro_cliente_aceites (
    id bigint generated always as identity primary key,
    solicitacao_id bigint not null references sigov.b2b_cadastro_cliente_solicitacoes(id),
    tipo varchar(80) not null,
    versao varchar(40) not null,
    aceito boolean not null,
    ip_origem varchar(80) null,
    user_agent varchar(250) null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_api_chaves (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    nome varchar(160) not null,
    prefixo varchar(20) not null,
    api_key_hash varchar(128) not null,
    escopos text not null,
    status varchar(40) not null,
    last_used_at timestamptz null,
    revoked_at timestamptz null,
    revoked_by bigint null,
    created_at timestamptz not null default now(),
    created_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_api_uso (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    api_chave_id bigint null references sigov.b2b_api_chaves(id),
    endpoint varchar(250) not null,
    status_code integer not null,
    duracao_ms bigint not null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_api_rate_limits (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    plano_id bigint null references sigov.b2b_planos(id),
    janela varchar(40) not null,
    limite bigint not null,
    status varchar(40) not null default 'ATIVO',
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_tenant_uso (
    tenant_id bigint primary key references sigov.tenant(id),
    usuarios_ativos bigint not null default 0,
    medicos_ativos bigint not null default 0,
    hospitais_ativos bigint not null default 0,
    plantoes_mes bigint not null default 0,
    requisicoes_api_mes bigint not null default 0,
    armazenamento_gb numeric(18,4) not null default 0,
    updated_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_solicitacoes_plano (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    tipo varchar(40) not null,
    plano_destino_codigo varchar(80) not null,
    motivo text not null,
    status varchar(40) not null,
    created_at timestamptz not null default now(),
    created_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_parceiros (
    id bigint generated always as identity primary key,
    nome varchar(250) not null,
    documento varchar(40) null,
    status varchar(40) not null default 'ATIVO',
    margem_percentual numeric(9,4) not null default 0,
    contrato_ativo boolean not null default false,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_parceiro_tenants (
    id bigint generated always as identity primary key,
    parceiro_id bigint not null references sigov.b2b_parceiros(id),
    tenant_id bigint not null references sigov.tenant(id),
    status varchar(40) not null default 'ATIVO',
    permissao_dados_sensiveis boolean not null default false,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_contratos (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    plano_id bigint not null references sigov.b2b_planos(id),
    status varchar(40) not null,
    inicio_vigencia timestamptz not null,
    fim_vigencia timestamptz null,
    valor_mensal numeric(18,2) not null default 0,
    taxa_setup numeric(18,2) not null default 0,
    uptime_contratado varchar(40) null,
    tempo_resposta_suporte varchar(80) null,
    tempo_resolucao_critico varchar(80) null,
    canal_atendimento varchar(120) null,
    janela_manutencao varchar(120) null,
    politica_backup text null,
    propriedade_dados text null,
    politica_exportacao_dados text null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_sla_incidentes (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    contrato_id bigint null references sigov.b2b_contratos(id),
    titulo varchar(250) not null,
    severidade varchar(40) not null,
    status varchar(40) not null,
    aberto_at timestamptz not null default now(),
    respondido_at timestamptz null,
    resolvido_at timestamptz null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_suporte_chamados (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    titulo varchar(250) not null,
    descricao text not null,
    prioridade varchar(40) not null,
    canal varchar(80) not null,
    critico boolean not null default false,
    status varchar(40) not null,
    sla_resumo varchar(250) null,
    created_at timestamptz not null default now(),
    created_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_suporte_chamado_eventos (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chamado_id bigint not null references sigov.b2b_suporte_chamados(id),
    tipo varchar(80) not null,
    descricao text not null,
    created_at timestamptz not null default now(),
    created_by bigint null,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_beta_feedbacks (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    titulo varchar(250) not null,
    descricao text null,
    severidade varchar(40) not null,
    status varchar(40) not null default 'ABERTO',
    satisfacao integer not null default 0,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_marketing_materiais (
    id bigint generated always as identity primary key,
    titulo varchar(250) not null,
    tipo varchar(80) not null,
    visibilidade varchar(40) not null,
    conteudo_resumo text null,
    ativo boolean not null default true,
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_telemetria_eventos (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    tipo_evento varchar(120) not null,
    entidade varchar(120) null,
    entidade_id bigint null,
    severidade varchar(40) not null default 'INFO',
    dados jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_telemetria_alertas (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    titulo varchar(250) not null,
    severidade varchar(40) not null,
    status varchar(40) not null default 'ABERTO',
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_telemetria_erros_criticos (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    fingerprint varchar(128) not null,
    mensagem_mascarada text not null,
    resolvido boolean not null default false,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_telemetria_endpoint_performance (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    endpoint varchar(250) not null,
    metodo varchar(20) not null,
    duracao_ms bigint not null,
    status_code integer not null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create table if not exists sigov.b2b_lgpd_consentimentos (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    titular_ref varchar(120) not null,
    finalidade varchar(160) not null,
    versao_politica varchar(40) not null,
    consentido boolean not null,
    created_at timestamptz not null default now(),
    reg_date timestamptz not null default now()
);

create index if not exists idx_b2b_planos_status_reg_date on sigov.b2b_planos (ativo, publico, reg_date);
create index if not exists idx_b2b_assinaturas_tenant_status on sigov.b2b_assinaturas (tenant_id, status, reg_date);
create index if not exists idx_b2b_white_label_dominio on sigov.b2b_tenant_white_label (dominio_customizado);
create index if not exists idx_b2b_white_label_subdominio on sigov.b2b_tenant_white_label (subdominio);
create index if not exists idx_b2b_cadastro_cnpj_status on sigov.b2b_cadastro_cliente_solicitacoes (cnpj, status, reg_date);
create index if not exists idx_b2b_api_chaves_tenant_hash on sigov.b2b_api_chaves (tenant_id, api_key_hash);
create index if not exists idx_b2b_api_uso_tenant_reg_date on sigov.b2b_api_uso (tenant_id, reg_date);
create index if not exists idx_b2b_parceiro_tenants_parceiro on sigov.b2b_parceiro_tenants (parceiro_id, tenant_id, status);
create index if not exists idx_b2b_contratos_tenant_status on sigov.b2b_contratos (tenant_id, status, reg_date);
create index if not exists idx_b2b_suporte_chamados_tenant_status on sigov.b2b_suporte_chamados (tenant_id, status, reg_date);
create index if not exists idx_b2b_alertas_status on sigov.b2b_telemetria_alertas (status, severidade, reg_date);
create index if not exists idx_b2b_endpoint_perf_tenant on sigov.b2b_telemetria_endpoint_performance (tenant_id, duracao_ms, reg_date);

insert into sigov.b2b_planos (codigo, nome, descricao, publico_alvo, valor_mensal, permite_white_label, permite_api, limite_usuarios, limite_medicos, limite_hospitais, limite_plantoes_mes, sla_resumo, ordem)
values
('ESSENCIAL', 'Plano Essencial', 'Operação inicial para escalas e plantões médicos.', 'Hospitais e clínicas em início de operação digital', 499, false, false, 5, 20, 2, 100, 'Suporte padrão', 10),
('PROFISSIONAL', 'Plano Profissional', 'Operação assistida com API limitada, relatórios avançados e white label básico.', 'Operações multiunidade em crescimento', 1499, true, true, 20, 100, 10, 500, 'Suporte prioritário', 20),
('ENTERPRISE_WHITE_LABEL', 'Enterprise White Label', 'White label completo, domínio customizado, API, webhooks, BI e SLA.', 'Redes de saúde e operações B2B', 4990, true, true, 999999, 999999, 999999, 999999, 'SLA contratual', 30),
('REVENDEDOR', 'Plano Revendedor', 'Console parceiro, tenants vinculados, margem, comissão e repasses.', 'Parceiros e consultorias', 7990, true, true, 999999, 999999, 999999, 999999, 'SLA B2B para parceiros', 40),
('CUSTOM', 'Plano Custom', 'Contrato personalizado, integrações específicas e infraestrutura dedicada opcional.', 'Projetos sob proposta', 0, true, true, 999999, 999999, 999999, 999999, 'SLA customizado', 50)
on conflict (codigo) do update set nome = excluded.nome, descricao = excluded.descricao, publico_alvo = excluded.publico_alvo, valor_mensal = excluded.valor_mensal, permite_white_label = excluded.permite_white_label, permite_api = excluded.permite_api, limite_usuarios = excluded.limite_usuarios, limite_medicos = excluded.limite_medicos, limite_hospitais = excluded.limite_hospitais, limite_plantoes_mes = excluded.limite_plantoes_mes, sla_resumo = excluded.sla_resumo, ordem = excluded.ordem;

insert into sigov.b2b_marketing_materiais (titulo, tipo, visibilidade, conteudo_resumo)
values
('One page comercial PlantãoPro', 'ONE_PAGE', 'parceiro', 'Resumo comercial para hospitais, redes de saúde e parceiros.'),
('Roteiro de demonstração white label', 'DEMO', 'interno', 'Fluxo de planos, self-service, white label, API, suporte e monitoramento.'),
('Argumentário para CTO', 'ARGUMENTARIO', 'parceiro', 'Segurança, isolamento por tenant, API keys, LGPD, auditoria e observabilidade.'),
('Proposta comercial modelo', 'PROPOSTA', 'interno', 'Modelo de proposta B2B com setup, SLA, revenda e customizações.')
on conflict do nothing;

DO $$
DECLARE
    table_name text;
    tables text[] := array[
        'tenants','tenant_configuracoes','tenant_modulos','tenant_parametros','tenant_dominios','tenant_onboarding','tenant_onboarding_checklist','tenant_auditoria_configuracoes','tenant_ambientes','tenant_status_historico',
        'tenant_white_label','white_label_temas','white_label_assets','white_label_textos','white_label_emails','white_label_parametros_mobile','white_label_dominios','white_label_publicacoes','white_label_historico_alteracoes',
        'cadastro_cliente_solicitacoes','cadastro_cliente_etapas','cadastro_cliente_validacoes','cadastro_cliente_convites','cadastro_cliente_pagamentos_iniciais','cadastro_cliente_aceites',
        'perfis','permissoes','modulos_sistema','acoes_sistema','perfil_permissoes','perfil_modulos','usuario_perfis','usuario_permissoes_especiais',
        'planos','plano_recursos','plano_modulos','plano_precos','plano_limites','plano_comparativo','plano_faq','plano_setup_taxas','plano_sla','plano_api_limites','plano_armazenamento_limites','upgrade_solicitacoes','downgrade_solicitacoes',
        'parceiros','parceiro_tenants','parceiro_planos','parceiro_comissoes','parceiro_repasses','parceiro_leads','parceiro_oportunidades','parceiro_margens','parceiro_contratos',
        'contratos','contrato_itens','contrato_slas','contrato_aceites','contrato_renovacoes','contrato_anexos','sla_eventos','sla_incidentes','sla_indicadores',
        'api_clientes','api_chaves','api_escopos','api_rate_limits','api_uso','api_webhooks','api_webhook_eventos','api_documentacao_topicos','api_documentacao_exemplos',
        'suporte_canais','suporte_chamados','suporte_chamado_eventos','suporte_sla','suporte_base_conhecimento','suporte_feedbacks',
        'beta_programas','beta_clientes','beta_feedbacks','beta_incidentes','marketing_casos_uso','marketing_materiais','campanhas_b2b','contatos_decisores',
        'telemetria_eventos','telemetria_metricas','telemetria_alertas','telemetria_healthchecks','telemetria_endpoint_performance','telemetria_tenant_uso','telemetria_erros_criticos',
        'tenant_parametros_operacionais','tenant_parametros_financeiros','tenant_parametros_notificacoes','tenant_parametros_lgpd','tenant_parametros_api','tenant_parametros_suporte',
        'lgpd_consentimentos','lgpd_politicas','lgpd_solicitacoes_titular','lgpd_eventos_privacidade'
    ];
BEGIN
    FOREACH table_name IN ARRAY tables LOOP
        EXECUTE format('create table if not exists plantaopro.%I (id bigint generated always as identity primary key, tenant_id bigint null, cliente_id bigint null, plano_id bigint null, parceiro_id bigint null, status varchar(60) not null default ''ATIVO'', codigo varchar(120) null, nome varchar(250) null, dominio varchar(250) null, subdominio varchar(120) null, api_key_hash varchar(128) null, dados jsonb not null default ''{}''::jsonb, reg_date timestamptz not null default now(), created_at timestamptz not null default now(), updated_at timestamptz null)', table_name);
        EXECUTE format('create index if not exists idx_plantaopro_%s_tenant_status on plantaopro.%I (tenant_id, status, reg_date)', table_name, table_name);
        EXECUTE format('create index if not exists idx_plantaopro_%s_cliente_status on plantaopro.%I (cliente_id, status, reg_date)', table_name, table_name);
        EXECUTE format('create index if not exists idx_plantaopro_%s_plano on plantaopro.%I (plano_id)', table_name, table_name);
        EXECUTE format('create index if not exists idx_plantaopro_%s_parceiro on plantaopro.%I (parceiro_id)', table_name, table_name);
    END LOOP;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_b2b_planos_limites_nao_negativos') THEN
        ALTER TABLE sigov.b2b_planos ADD CONSTRAINT ck_b2b_planos_limites_nao_negativos CHECK (limite_usuarios >= 0 and limite_medicos >= 0 and limite_hospitais >= 0 and limite_plantoes_mes >= 0);
    END IF;
END $$;
