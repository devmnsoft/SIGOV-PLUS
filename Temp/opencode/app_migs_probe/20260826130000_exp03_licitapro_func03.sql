-- EXP03 - LicitaPro IA integrado ao FUNC03 Compras, Licitações, Contratos e Atas.
-- Estrutura aditiva, idempotente, multi-tenant e sem dados simulados.
create table if not exists sigov.compras_licitapro_fonte (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 nome varchar(160) not null, tipo varchar(30) not null, endpoint_url text, configurada boolean not null default false,
 ativa boolean not null default true, ultima_sincronizacao_at timestamptz, created_at timestamptz not null default now(), created_by bigint not null,
 updated_at timestamptz, updated_by bigint, constraint ck_clp_fonte_tipo check(tipo in('PNCP','PORTAL_PUBLICO','OUTRA_OFICIAL')),
 constraint ck_clp_fonte_config check(not configurada or endpoint_url is not null), unique(tenant_id,entidade_id,nome)
);
create table if not exists sigov.compras_licitapro_importacao (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fonte_id bigint not null references sigov.compras_licitapro_fonte(id),
 versao integer not null, status varchar(25) not null default 'PENDENTE', iniciada_at timestamptz not null default now(), concluida_at timestamptz,
 itens_lidos integer not null default 0, itens_importados integer not null default 0, erro_sanitizado text, created_by bigint not null,
 constraint ck_clp_import_status check(status in('PENDENTE','PROCESSANDO','CONCLUIDA','FALHA','INDISPONIVEL')),
 constraint ck_clp_import_qtd check(itens_lidos>=0 and itens_importados>=0 and itens_importados<=itens_lidos), unique(tenant_id,entidade_id,fonte_id,versao)
);
create table if not exists sigov.compras_licitapro_oportunidade (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fonte_id bigint not null references sigov.compras_licitapro_fonte(id),
 importacao_id bigint references sigov.compras_licitapro_importacao(id), identificador_externo varchar(180) not null, numero varchar(120) not null,
 objeto text not null, modalidade varchar(100) not null, orgao varchar(200), data_publicacao date not null, data_limite date,
 status varchar(25) not null default 'ABERTA', url_oficial text, valor_estimado numeric(18,2), processo_id bigint references sigov.compras_processo(id),
 versao_fonte integer not null default 1, payload_hash varchar(64), created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_oport_status check(status in('ABERTA','VINCULADA','VENCIDA','CANCELADA','INDISPONIVEL')),
 constraint ck_clp_oport_datas check(data_limite is null or data_limite>=data_publicacao), constraint ck_clp_oport_valor check(valor_estimado is null or valor_estimado>=0),
 unique(tenant_id,entidade_id,fonte_id,identificador_externo)
);
create table if not exists sigov.compras_licitapro_documento (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fornecedor_id bigint not null references sigov.compras_fornecedor(id),
 tipo varchar(80) not null, titulo varchar(180) not null, validade date, status varchar(25) not null default 'PENDENTE', referencia_documental text,
 aprovado_por bigint, aprovado_at timestamptz, created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_doc_status check(status in('PENDENTE','EM_ANALISE','APROVADO','REPROVADO','VENCIDO')),
 constraint ck_clp_doc_aprovado check(status<>'APROVADO' or (validade is not null and referencia_documental is not null))
);
create table if not exists sigov.compras_licitapro_checklist (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fornecedor_id bigint not null references sigov.compras_fornecedor(id),
 processo_id bigint not null references sigov.compras_processo(id), status varchar(20) not null default 'PENDENTE', concluido_at timestamptz, concluido_por bigint,
 created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_check_status check(status in('PENDENTE','EM_ANALISE','CONCLUIDO','BLOQUEADO')), unique(tenant_id,entidade_id,fornecedor_id,processo_id)
);
create table if not exists sigov.compras_licitapro_checklist_item (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, checklist_id bigint not null references sigov.compras_licitapro_checklist(id) on delete cascade,
 requisito text not null, obrigatorio boolean not null default true, status varchar(20) not null default 'PENDENTE', justificativa text, documento_id bigint references sigov.compras_licitapro_documento(id),
 created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_item_status check(status in('PENDENTE','ATENDIDO','NAO_APLICAVEL','BLOQUEADO')),
 constraint ck_clp_item_just check(not(obrigatorio and status in('NAO_APLICAVEL','BLOQUEADO')) or justificativa is not null)
);
create table if not exists sigov.compras_licitapro_analise (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, processo_id bigint not null references sigov.compras_processo(id),
 oportunidade_id bigint references sigov.compras_licitapro_oportunidade(id), status varchar(20) not null default 'RASCUNHO', observacoes text, riscos text,
 sugestao text, justificativa_responsavel text, responsavel_id bigint not null, created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_analise_status check(status in('RASCUNHO','EM_REVISAO','CONCLUIDA'))
);
create table if not exists sigov.compras_licitapro_criterio (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, analise_id bigint not null references sigov.compras_licitapro_analise(id) on delete cascade,
 nome varchar(160) not null, peso_percentual numeric(5,2) not null, aderencia_percentual numeric(5,2) not null, score numeric(12,4) not null, explicacao text not null,
 created_at timestamptz not null default now(), created_by bigint not null,
 constraint ck_clp_criterio_percent check(peso_percentual between 0 and 100 and aderencia_percentual between 0 and 100), constraint ck_clp_criterio_score check(score>=0)
);
create table if not exists sigov.compras_licitapro_agenda (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, oportunidade_id bigint references sigov.compras_licitapro_oportunidade(id),
 processo_id bigint not null references sigov.compras_processo(id), fornecedor_id bigint not null references sigov.compras_fornecedor(id), titulo varchar(180) not null,
 prazo_at timestamptz not null, status varchar(25) not null default 'PREPARACAO', responsavel_id bigint not null, alerta_bloqueio text,
 contrato_id bigint references sigov.compras_contrato(id), created_at timestamptz not null default now(), created_by bigint not null, updated_at timestamptz, updated_by bigint,
 constraint ck_clp_agenda_status check(status in('PREPARACAO','PRONTA','ENVIADA','VENCIDA','CANCELADA','CONQUISTADA'))
);
create table if not exists sigov.compras_licitapro_alerta (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fornecedor_id bigint references sigov.compras_fornecedor(id),
 documento_id bigint references sigov.compras_licitapro_documento(id), agenda_id bigint references sigov.compras_licitapro_agenda(id), tipo varchar(40) not null,
 mensagem text not null, status varchar(20) not null default 'ABERTO', vencimento_at timestamptz, created_at timestamptz not null default now(), created_by bigint not null,
 constraint ck_clp_alerta_status check(status in('ABERTO','CIENTE','RESOLVIDO'))
);
create table if not exists sigov.compras_licitapro_sincronizacao (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, fonte_id bigint not null references sigov.compras_licitapro_fonte(id),
 importacao_id bigint references sigov.compras_licitapro_importacao(id), status varchar(25) not null, tentativa_at timestamptz not null default now(), finalizada_at timestamptz,
 erro_sanitizado text, correlation_id varchar(120), created_by bigint not null, constraint ck_clp_sync_status check(status in('PROCESSANDO','CONCLUIDA','FALHA','INDISPONIVEL','NAO_CONFIGURADA'))
);
create table if not exists sigov.compras_licitapro_auditoria (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, entidade varchar(80) not null, registro_id bigint,
 acao varchar(60) not null, detalhes jsonb, usuario_id bigint not null, ocorrido_at timestamptz not null default now(), correlation_id varchar(120)
);
create index if not exists ix_clp_oport_status_fonte_publicacao on sigov.compras_licitapro_oportunidade(tenant_id,entidade_id,status,fonte_id,data_publicacao desc);
create index if not exists ix_clp_oport_limite_modalidade on sigov.compras_licitapro_oportunidade(tenant_id,entidade_id,data_limite,modalidade);
create index if not exists ix_clp_oport_processo on sigov.compras_licitapro_oportunidade(tenant_id,entidade_id,processo_id);
create index if not exists ix_clp_doc_fornecedor_status on sigov.compras_licitapro_documento(tenant_id,entidade_id,fornecedor_id,status,validade);
create index if not exists ix_clp_check_processo_status on sigov.compras_licitapro_checklist(tenant_id,entidade_id,processo_id,status);
create index if not exists ix_clp_agenda_status_prazo on sigov.compras_licitapro_agenda(tenant_id,entidade_id,status,prazo_at);
create index if not exists ix_clp_sync_fonte_data on sigov.compras_licitapro_sincronizacao(tenant_id,entidade_id,fonte_id,tentativa_at desc);
create index if not exists ix_clp_auditoria_data on sigov.compras_licitapro_auditoria(tenant_id,entidade_id,ocorrido_at desc,acao);
do $$ declare p text; begin foreach p in array array['COMPRAS_LICITAPRO_DASHBOARD_VIEW','COMPRAS_LICITAPRO_FONTE_VIEW','COMPRAS_LICITAPRO_FONTE_MANAGE','COMPRAS_LICITAPRO_OPORTUNIDADE_VIEW','COMPRAS_LICITAPRO_OPORTUNIDADE_MANAGE','COMPRAS_LICITAPRO_FORNECEDOR_PORTAL_VIEW','COMPRAS_LICITAPRO_FORNECEDOR_PORTAL_MANAGE','COMPRAS_LICITAPRO_DOCUMENTO_VIEW','COMPRAS_LICITAPRO_DOCUMENTO_MANAGE','COMPRAS_LICITAPRO_CHECKLIST_VIEW','COMPRAS_LICITAPRO_CHECKLIST_MANAGE','COMPRAS_LICITAPRO_ANALISE_VIEW','COMPRAS_LICITAPRO_ANALISE_MANAGE','COMPRAS_LICITAPRO_AGENDA_VIEW','COMPRAS_LICITAPRO_AGENDA_MANAGE','COMPRAS_LICITAPRO_RELATORIO_EXPORT','COMPRAS_LICITAPRO_AUDITORIA_VIEW'] loop
 insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted) select 'compras',p,'compras.licitapro.'||lower(regexp_replace(regexp_replace(p,'^COMPRAS_LICITAPRO_',''),'_(VIEW|MANAGE|EXPORT)$','')),case when p like '%_VIEW' then 'visualizar' when p like '%_EXPORT' then 'exportar' else 'gerenciar' end,'Permissão LicitaPro IA integrada ao FUNC03',true,false where not exists(select 1 from sigov.permissao where chave=p);
end loop; end $$;
