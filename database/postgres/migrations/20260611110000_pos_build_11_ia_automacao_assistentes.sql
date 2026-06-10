

alter table sigov.tenant_uso_mensal add column if not exists ia_interacoes int not null default 0;
alter table sigov.tenant_uso_mensal add column if not exists ia_tokens_entrada int not null default 0;
alter table sigov.tenant_uso_mensal add column if not exists ia_tokens_saida int not null default 0;
alter table sigov.tenant_uso_mensal add column if not exists ia_documentos_classificados int not null default 0;
alter table sigov.tenant_uso_mensal add column if not exists ia_relatorios_gerados int not null default 0;
alter table sigov.tenant_uso_mensal add column if not exists ia_automacoes_executadas int not null default 0;

-- Pós-Build 11: IA, automação inteligente, assistentes operacionais e análise preditiva
create table if not exists sigov.ia_configuracao_tenant (
    tenant_id bigint primary key,
    ia_habilitada boolean not null default false,
    permitir_envio_externo boolean not null default false,
    mascarar_dados_sensiveis boolean not null default true,
    exigir_confirmacao_acao_critica boolean not null default true,
    provedor_padrao_codigo varchar(80) null,
    limite_interacoes_mes int null,
    limite_tokens_mes int null,
    updated_at timestamptz null
);

create table if not exists sigov.ia_provedor (
    id bigserial primary key,
    codigo varchar(80) not null unique,
    nome varchar(200) not null,
    tipo varchar(40) not null,
    endpoint_url text null,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_assistente (
    id bigserial primary key,
    codigo varchar(80) not null unique,
    nome varchar(200) not null,
    descricao text null,
    tipo varchar(80) not null,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_assistente_modulo (
    assistente_id bigint not null references sigov.ia_assistente(id),
    modulo_codigo varchar(80) not null,
    primary key(assistente_id, modulo_codigo)
);

create table if not exists sigov.ia_prompt_template (
    id bigserial primary key,
    tenant_id bigint null,
    codigo varchar(120) not null,
    nome varchar(200) not null,
    modulo_codigo varchar(80) null,
    tipo varchar(80) not null,
    template text not null,
    exige_confirmacao boolean not null default false,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.ia_execucao (
    id bigserial primary key,
    tenant_id bigint not null,
    usuario_id bigint null,
    assistente_codigo varchar(80) null,
    modulo_codigo varchar(80) null,
    tipo varchar(80) not null,
    origem varchar(80) null,
    origem_id bigint null,
    prompt text not null,
    resposta text null,
    status varchar(40) not null default 'PENDENTE',
    provedor_codigo varchar(80) null,
    tokens_entrada int null,
    tokens_saida int null,
    custo_estimado numeric(14,6) null,
    erro text null,
    correlation_id uuid not null,
    created_at timestamptz not null default now(),
    concluida_at timestamptz null
);

create table if not exists sigov.ia_execucao_contexto (
    id bigserial primary key,
    execucao_id bigint not null references sigov.ia_execucao(id),
    chave varchar(120) not null,
    valor text null,
    sensivel boolean not null default false,
    mascarado boolean not null default false
);

create table if not exists sigov.ia_sugestao (
    id bigserial primary key,
    tenant_id bigint not null,
    execucao_id bigint null references sigov.ia_execucao(id),
    modulo_codigo varchar(80) null,
    origem varchar(80) null,
    origem_id bigint null,
    titulo varchar(200) not null,
    descricao text not null,
    tipo varchar(80) not null,
    prioridade varchar(40) not null default 'MEDIA',
    status varchar(40) not null default 'PENDENTE',
    exige_confirmacao boolean not null default true,
    criada_at timestamptz not null default now(),
    aplicada_at timestamptz null,
    rejeitada_at timestamptz null,
    usuario_decisao_id bigint null
);

create table if not exists sigov.ia_automacao (
    id bigserial primary key,
    tenant_id bigint not null,
    codigo varchar(120) not null,
    nome varchar(200) not null,
    descricao text null,
    modulo_codigo varchar(80) null,
    gatilho varchar(120) not null,
    condicao_json jsonb null,
    acao_json jsonb not null,
    exige_confirmacao boolean not null default true,
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique(tenant_id, codigo)
);

create table if not exists sigov.ia_automacao_execucao (
    id bigserial primary key,
    automacao_id bigint not null references sigov.ia_automacao(id),
    tenant_id bigint not null,
    status varchar(40) not null default 'PENDENTE',
    entrada_json jsonb null,
    resultado_json jsonb null,
    erro text null,
    correlation_id uuid not null,
    created_at timestamptz not null default now(),
    concluida_at timestamptz null
);

create table if not exists sigov.ia_classificacao_documento (
    id bigserial primary key,
    tenant_id bigint not null,
    documento_id bigint null,
    tipo_sugerido varchar(120) null,
    confianca numeric(7,4) null,
    metadados_json jsonb null,
    revisado boolean not null default false,
    revisado_por bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_extracao_campo (
    id bigserial primary key,
    tenant_id bigint not null,
    documento_id bigint null,
    campo varchar(120) not null,
    valor text null,
    confianca numeric(7,4) null,
    revisado boolean not null default false,
    revisado_por bigint null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_alerta_inteligente (
    id bigserial primary key,
    tenant_id bigint not null,
    modulo_codigo varchar(80) null,
    tipo varchar(80) not null,
    titulo varchar(200) not null,
    mensagem text not null,
    prioridade varchar(40) not null default 'MEDIA',
    origem varchar(80) null,
    origem_id bigint null,
    lido boolean not null default false,
    resolvido boolean not null default false,
    created_at timestamptz not null default now(),
    resolvido_at timestamptz null
);

create table if not exists sigov.ia_modelo_predicao (
    id bigserial primary key,
    codigo varchar(120) not null unique,
    nome varchar(200) not null,
    descricao text null,
    modulo_codigo varchar(80) null,
    tipo varchar(80) not null,
    ativo boolean not null default true,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_predicao_resultado (
    id bigserial primary key,
    tenant_id bigint not null,
    modelo_codigo varchar(120) not null,
    origem varchar(80) null,
    origem_id bigint null,
    score numeric(7,4) not null,
    classificacao varchar(80) null,
    explicacao text null,
    dados_json jsonb null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_feedback_usuario (
    id bigserial primary key,
    tenant_id bigint not null,
    execucao_id bigint null references sigov.ia_execucao(id),
    sugestao_id bigint null references sigov.ia_sugestao(id),
    usuario_id bigint null,
    avaliacao int null,
    comentario text null,
    util boolean null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.ia_consumo (
    id bigserial primary key,
    tenant_id bigint not null,
    competencia date not null,
    interacoes int not null default 0,
    tokens_entrada int not null default 0,
    tokens_saida int not null default 0,
    custo_estimado numeric(14,6) not null default 0,
    created_at timestamptz not null default now(),
    unique(tenant_id, competencia)
);

create index if not exists idx_ia_execucao_tenant_created on sigov.ia_execucao(tenant_id, created_at desc);
create index if not exists idx_ia_sugestao_tenant_status on sigov.ia_sugestao(tenant_id, status, criada_at desc);
create index if not exists idx_ia_automacao_tenant on sigov.ia_automacao(tenant_id, ativo);
create index if not exists idx_ia_alerta_tenant on sigov.ia_alerta_inteligente(tenant_id, resolvido, created_at desc);
create index if not exists idx_ia_predicao_tenant on sigov.ia_predicao_resultado(tenant_id, created_at desc);
create index if not exists idx_ia_consumo_tenant_comp on sigov.ia_consumo(tenant_id, competencia desc);

insert into sigov.ia_provedor(codigo,nome,tipo,endpoint_url,ativo) values
('INTERNO','Provider interno heurístico','INTERNO',null,true),
('LOCAL','Provider local configurável','LOCAL',null,true),
('OPENAI','OpenAI configurável','OPENAI',null,false),
('AZURE_OPENAI','Azure OpenAI configurável','AZURE_OPENAI',null,false)
on conflict(codigo) do update set nome=excluded.nome,tipo=excluded.tipo,endpoint_url=excluded.endpoint_url,ativo=excluded.ativo;

insert into sigov.ia_assistente(codigo,nome,descricao,tipo,ativo) values
('ASSISTENTE_GERAL','Assistente Geral','Apoio operacional geral por tenant.','GERAL',true),
('ASSISTENTE_TRIBUTARIO','Assistente Tributário','Apoio a análises fiscais e arrecadação.','TRIBUTARIO',true),
('ASSISTENTE_FINANCEIRO','Assistente Financeiro','Apoio a fluxo de caixa, cobrança e inadimplência.','FINANCEIRO',true),
('ASSISTENTE_GED','Assistente GED','Resumo, classificação e extração documental.','GED',true),
('ASSISTENTE_COMERCIAL','Assistente Comercial','Apoio a clientes, leads e oportunidades.','COMERCIAL',true),
('ASSISTENTE_INDUSTRIA','Assistente Indústria','Apoio ao chão de fábrica e produção.','INDUSTRIA',true),
('ASSISTENTE_OS','Assistente OS','Apoio a ordens de serviço.','OS',true),
('ASSISTENTE_JURIDICO','Assistente Jurídico','Geração assistida de pareceres.','JURIDICO',true),
('ASSISTENTE_CONTRATOS','Assistente Contratos','Resumo e risco contratual.','CONTRATOS',true),
('ASSISTENTE_SUPORTE','Assistente Suporte','Apoio a atendimento e suporte.','SUPORTE',true)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,tipo=excluded.tipo,ativo=excluded.ativo;

insert into sigov.ia_prompt_template(tenant_id,codigo,nome,modulo_codigo,tipo,template,exige_confirmacao,ativo)
select v.tenant_id,v.codigo,v.nome,v.modulo_codigo,v.tipo,v.template,v.exige_confirmacao,v.ativo
from (values
(null,'resumo_documento','Resumo de documento','ged','RESUMO','Resuma o documento, indique pontos relevantes e informe se houver dados insuficientes.',false,true),
(null,'resumo_processo','Resumo de processo','protocolo','RESUMO','Resuma o processo e sugira próximos passos sem executar ações críticas.',false,true),
(null,'resumo_os','Resumo de OS','ordem_servico','RESUMO','Resuma o histórico da OS e a próxima ação segura.',false,true),
(null,'resumo_contrato','Resumo de contrato','contrato','RESUMO','Resuma contrato, vigência, cláusulas e riscos.',false,true),
(null,'classificar_documento','Classificar documento','ged','CLASSIFICACAO','Classifique por palavras-chave e indique confiança.',false,true),
(null,'extrair_dados_documento','Extrair dados de documento','ged','EXTRACAO','Extraia número, datas, valores e partes, mascarando dados sensíveis.',false,true),
(null,'gerar_relatorio_financeiro','Gerar relatório financeiro','financeiro_empresarial','RELATORIO','Gere relatório textual financeiro com riscos e oportunidades.',false,true),
(null,'gerar_parecer_juridico','Gerar parecer jurídico','juridico','PARECER','Rascunhe parecer para revisão humana obrigatória.',true,true),
(null,'gerar_despacho_protocolo','Gerar despacho de protocolo','protocolo','DESPACHO','Rascunhe despacho administrativo para revisão humana.',true,true),
(null,'sugerir_acoes_cobranca','Sugerir ações de cobrança','financeiro_empresarial','SUGESTAO','Sugira ações de cobrança sem baixa financeira automática.',true,true),
(null,'prever_inadimplencia','Prever inadimplência','financeiro_empresarial','ANALISE','Estime risco de inadimplência por regras.',false,true),
(null,'prever_atraso_os','Prever atraso OS','ordem_servico','ANALISE','Estime risco de atraso em OS.',false,true),
(null,'prever_ruptura_estoque','Prever ruptura estoque','estoque_compras','ANALISE','Estime risco de ruptura por saldo e estoque mínimo.',false,true),
(null,'sugerir_reposicao_estoque','Sugerir reposição','estoque_compras','SUGESTAO','Sugira compra/reposição com confirmação humana.',true,true),
(null,'analisar_fluxo_caixa','Analisar fluxo de caixa','financeiro_empresarial','ANALISE','Analise tendências e alertas no fluxo de caixa.',false,true),
(null,'analisar_producao_atrasada','Analisar produção atrasada','industria_producao','ANALISE','Identifique risco e causa provável de atraso produtivo.',false,true)
) as v(tenant_id,codigo,nome,modulo_codigo,tipo,template,exige_confirmacao,ativo)
where not exists (
    select 1 from sigov.ia_prompt_template t
    where ((t.tenant_id is null and v.tenant_id is null) or t.tenant_id = v.tenant_id)
      and t.codigo = v.codigo
);

insert into sigov.ia_modelo_predicao(codigo,nome,descricao,modulo_codigo,tipo,ativo) values
('inadimplencia_cliente','Inadimplência de cliente','Modelo heurístico inicial.','financeiro_empresarial','INADIMPLENCIA',true),
('atraso_ordem_servico','Atraso em ordem de serviço','Modelo heurístico inicial.','ordem_servico','ATRASO_OS',true),
('ruptura_estoque','Ruptura de estoque','Modelo heurístico inicial.','estoque_compras','ESTOQUE_RUPTURA',true),
('risco_contrato','Risco contratual','Modelo heurístico inicial.','contrato','CONTRATO_RISCO',true),
('atraso_producao','Atraso de produção','Modelo heurístico inicial.','industria_producao','PRODUCAO_ATRASO',true),
('queda_arrecadacao','Queda de arrecadação','Modelo heurístico inicial.','tributario','ARRECADACAO',true),
('churn_saas','Churn SaaS','Modelo heurístico inicial.','operacao-saas','CHURN',true)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,modulo_codigo=excluded.modulo_codigo,tipo=excluded.tipo,ativo=excluded.ativo;

insert into sigov.modulo_saas(codigo,nome,descricao,categoria,ordem,rota_base,icone,ativo) values
('ia_assistente','IA Assistente','Assistentes inteligentes por módulo, seguros e auditáveis.','Inteligência',900,'/IA/Assistente','bi-robot',true),
('ia_documental','IA Documental','Resumo, classificação e extração estruturada de documentos.','Inteligência',901,'/IA/Documental','bi-file-earmark-text',true),
('ia_relatorios','IA Relatórios','Geração assistida de relatórios textuais.','Inteligência',902,'/IA/Relatorios','bi-bar-chart-line',true),
('ia_automacoes','IA Automações','Sugestões, alertas e workflows com confirmação humana.','Inteligência',903,'/IA/Automacoes','bi-diagram-3',true),
('ia_predicoes','IA Predições','Análise preditiva inicial baseada em regras.','Inteligência',904,'/IA/Predicoes','bi-graph-up-arrow',true)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,categoria=excluded.categoria,ordem=excluded.ordem,rota_base=excluded.rota_base,icone=excluded.icone,ativo=excluded.ativo;

insert into sigov.saas_addon(codigo,nome,descricao,tipo_addon,modulo_codigo,preco,periodicidade) values
('ia_1000_interacoes','IA 1.000 interações','Pacote mensal adicional de 1.000 interações de IA.','IA_CONSUMO','ia_assistente',null,'MENSAL'),
('ia_10000_interacoes','IA 10.000 interações','Pacote mensal adicional de 10.000 interações de IA.','IA_CONSUMO','ia_assistente',null,'MENSAL'),
('ia_ocr_avancado','IA OCR avançado','Capacidades avançadas de OCR e extração.','IA_RECURSO','ia_documental',null,'MENSAL'),
('ia_relatorios_avancados','IA relatórios avançados','Relatórios assistidos avançados.','IA_RECURSO','ia_relatorios',null,'MENSAL'),
('ia_automacoes_avancadas','IA automações avançadas','Workflows inteligentes avançados.','IA_RECURSO','ia_automacoes',null,'MENSAL')
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,tipo_addon=excluded.tipo_addon,modulo_codigo=excluded.modulo_codigo;

insert into sigov.tenant_modulo_pacote(codigo,nome,descricao,modulos_json) values
('AI_STARTER','AI Starter','Assistente e relatórios por IA.','["ia_assistente","ia_relatorios"]'::jsonb),
('AI_DOCUMENTAL','AI Documental','Assistente, documental, GED e OCR.','["ia_assistente","ia_documental","ged","ocr"]'::jsonb),
('AI_ENTERPRISE','AI Enterprise','Base completa de IA operacional.','["ia_assistente","ia_documental","ia_relatorios","ia_automacoes","ia_predicoes","integracoes"]'::jsonb),
('BUSINESS_FULL_AI','Business Full AI','Business Full com IA.','["comercial","financeiro_empresarial","estoque_compras","ordem_servico","ia_assistente","ia_relatorios","ia_automacoes"]'::jsonb),
('GOV_FULL_AI','Gov Full AI','Governo com IA documental, relatórios e automações.','["tributario","protocolo","ged","contratos","financeiro_publico","ia_assistente","ia_documental","ia_relatorios","ia_automacoes"]'::jsonb)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,modulos_json=excluded.modulos_json;

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo) values
('ia','dashboard','visualizar','ia.dashboard.visualizar','Visualizar dashboard IA',true),
('ia','assistente','acessar','ia.assistente.acessar','Acessar assistente IA',true),
('ia','assistente','executar','ia.assistente.executar','Executar assistente IA',true),
('ia','execucoes','visualizar','ia.execucoes.visualizar','Visualizar execuções IA',true),
('ia','sugestoes','visualizar','ia.sugestoes.visualizar','Visualizar sugestões IA',true),
('ia','sugestoes','aprovar','ia.sugestoes.aprovar','Aprovar sugestões IA',true),
('ia','sugestoes','aplicar','ia.sugestoes.aplicar','Aplicar sugestões IA',true),
('ia','sugestoes','rejeitar','ia.sugestoes.rejeitar','Rejeitar sugestões IA',true),
('ia','documental','resumir','ia.documental.resumir','Resumir documentos por IA',true),
('ia','documental','classificar','ia.documental.classificar','Classificar documentos por IA',true),
('ia','documental','extrair','ia.documental.extrair','Extrair campos por IA',true),
('ia','relatorios','gerar','ia.relatorios.gerar','Gerar relatórios por IA',true),
('ia','automacoes','visualizar','ia.automacoes.visualizar','Visualizar automações IA',true),
('ia','automacoes','criar','ia.automacoes.criar','Criar automações IA',true),
('ia','automacoes','editar','ia.automacoes.editar','Editar automações IA',true),
('ia','automacoes','executar','ia.automacoes.executar','Executar automações IA',true),
('ia','alertas','visualizar','ia.alertas.visualizar','Visualizar alertas IA',true),
('ia','alertas','resolver','ia.alertas.resolver','Resolver alertas IA',true),
('ia','predicoes','visualizar','ia.predicoes.visualizar','Visualizar predições IA',true),
('ia','predicoes','executar','ia.predicoes.executar','Executar predições IA',true),
('ia','configuracao','visualizar','ia.configuracao.visualizar','Visualizar configuração IA',true),
('ia','configuracao','editar','ia.configuracao.editar','Editar configuração IA',true),
('ia','consumo','visualizar','ia.consumo.visualizar','Visualizar consumo IA',true),
('ia','consumo','recalcular','ia.consumo.recalcular','Recalcular consumo IA',true)
on conflict(chave) do update set modulo=excluded.modulo,recurso=excluded.recurso,acao=excluded.acao,descricao=excluded.descricao,ativo=excluded.ativo;
