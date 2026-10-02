-- Convergência aditiva do contrato completo do LicitaPro.
-- Esta versão não foi registrada quando o diagnóstico anterior abortou sua transação.
-- O ledger sigov.schema_migrations é administrado exclusivamente pelo MigrationRunner.

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


-- Reparar relações parciais sem converter nem remover dados.
do $$
declare mismatch record;
begin
    select required.table_name, required.column_name, c.data_type ||
           case when c.data_type='character varying' then '(' || c.character_maximum_length || ')' when c.data_type='numeric' then '(' || c.numeric_precision || ',' || c.numeric_scale || ')' else '' end actual_type,
           required.expected_type
      into mismatch
      from (values
        ('fonte','id','bigint'),
        ('fonte','tenant_id','bigint'),
        ('fonte','entidade_id','bigint'),
        ('fonte','nome','character varying(160)'),
        ('fonte','tipo','character varying(30)'),
        ('fonte','endpoint_url','text'),
        ('fonte','configurada','boolean'),
        ('fonte','ativa','boolean'),
        ('fonte','ultima_sincronizacao_at','timestamp with time zone'),
        ('fonte','created_at','timestamp with time zone'),
        ('fonte','created_by','bigint'),
        ('fonte','updated_at','timestamp with time zone'),
        ('fonte','updated_by','bigint'),
        ('importacao','id','bigint'),
        ('importacao','tenant_id','bigint'),
        ('importacao','entidade_id','bigint'),
        ('importacao','fonte_id','bigint'),
        ('importacao','versao','integer'),
        ('importacao','status','character varying(25)'),
        ('importacao','iniciada_at','timestamp with time zone'),
        ('importacao','concluida_at','timestamp with time zone'),
        ('importacao','itens_lidos','integer'),
        ('importacao','itens_importados','integer'),
        ('importacao','erro_sanitizado','text'),
        ('importacao','created_by','bigint'),
        ('oportunidade','id','bigint'),
        ('oportunidade','tenant_id','bigint'),
        ('oportunidade','entidade_id','bigint'),
        ('oportunidade','fonte_id','bigint'),
        ('oportunidade','importacao_id','bigint'),
        ('oportunidade','identificador_externo','character varying(180)'),
        ('oportunidade','numero','character varying(120)'),
        ('oportunidade','objeto','text'),
        ('oportunidade','modalidade','character varying(100)'),
        ('oportunidade','orgao','character varying(200)'),
        ('oportunidade','data_publicacao','date'),
        ('oportunidade','data_limite','date'),
        ('oportunidade','status','character varying(25)'),
        ('oportunidade','url_oficial','text'),
        ('oportunidade','valor_estimado','numeric(18,2)'),
        ('oportunidade','processo_id','bigint'),
        ('oportunidade','versao_fonte','integer'),
        ('oportunidade','payload_hash','character varying(64)'),
        ('oportunidade','created_at','timestamp with time zone'),
        ('oportunidade','created_by','bigint'),
        ('oportunidade','updated_at','timestamp with time zone'),
        ('oportunidade','updated_by','bigint'),
        ('documento','id','bigint'),
        ('documento','tenant_id','bigint'),
        ('documento','entidade_id','bigint'),
        ('documento','fornecedor_id','bigint'),
        ('documento','tipo','character varying(80)'),
        ('documento','titulo','character varying(180)'),
        ('documento','validade','date'),
        ('documento','status','character varying(25)'),
        ('documento','referencia_documental','text'),
        ('documento','aprovado_por','bigint'),
        ('documento','aprovado_at','timestamp with time zone'),
        ('documento','created_at','timestamp with time zone'),
        ('documento','created_by','bigint'),
        ('documento','updated_at','timestamp with time zone'),
        ('documento','updated_by','bigint'),
        ('checklist','id','bigint'),
        ('checklist','tenant_id','bigint'),
        ('checklist','entidade_id','bigint'),
        ('checklist','fornecedor_id','bigint'),
        ('checklist','processo_id','bigint'),
        ('checklist','status','character varying(20)'),
        ('checklist','concluido_at','timestamp with time zone'),
        ('checklist','concluido_por','bigint'),
        ('checklist','created_at','timestamp with time zone'),
        ('checklist','created_by','bigint'),
        ('checklist','updated_at','timestamp with time zone'),
        ('checklist','updated_by','bigint'),
        ('checklist_item','id','bigint'),
        ('checklist_item','tenant_id','bigint'),
        ('checklist_item','entidade_id','bigint'),
        ('checklist_item','checklist_id','bigint'),
        ('checklist_item','requisito','text'),
        ('checklist_item','obrigatorio','boolean'),
        ('checklist_item','status','character varying(20)'),
        ('checklist_item','justificativa','text'),
        ('checklist_item','documento_id','bigint'),
        ('checklist_item','created_at','timestamp with time zone'),
        ('checklist_item','created_by','bigint'),
        ('checklist_item','updated_at','timestamp with time zone'),
        ('checklist_item','updated_by','bigint'),
        ('analise','id','bigint'),
        ('analise','tenant_id','bigint'),
        ('analise','entidade_id','bigint'),
        ('analise','processo_id','bigint'),
        ('analise','oportunidade_id','bigint'),
        ('analise','status','character varying(20)'),
        ('analise','observacoes','text'),
        ('analise','riscos','text'),
        ('analise','sugestao','text'),
        ('analise','justificativa_responsavel','text'),
        ('analise','responsavel_id','bigint'),
        ('analise','created_at','timestamp with time zone'),
        ('analise','created_by','bigint'),
        ('analise','updated_at','timestamp with time zone'),
        ('analise','updated_by','bigint'),
        ('criterio','id','bigint'),
        ('criterio','tenant_id','bigint'),
        ('criterio','entidade_id','bigint'),
        ('criterio','analise_id','bigint'),
        ('criterio','nome','character varying(160)'),
        ('criterio','peso_percentual','numeric(5,2)'),
        ('criterio','aderencia_percentual','numeric(5,2)'),
        ('criterio','score','numeric(12,4)'),
        ('criterio','explicacao','text'),
        ('criterio','created_at','timestamp with time zone'),
        ('criterio','created_by','bigint'),
        ('agenda','id','bigint'),
        ('agenda','tenant_id','bigint'),
        ('agenda','entidade_id','bigint'),
        ('agenda','oportunidade_id','bigint'),
        ('agenda','processo_id','bigint'),
        ('agenda','fornecedor_id','bigint'),
        ('agenda','titulo','character varying(180)'),
        ('agenda','prazo_at','timestamp with time zone'),
        ('agenda','status','character varying(25)'),
        ('agenda','responsavel_id','bigint'),
        ('agenda','alerta_bloqueio','text'),
        ('agenda','contrato_id','bigint'),
        ('agenda','created_at','timestamp with time zone'),
        ('agenda','created_by','bigint'),
        ('agenda','updated_at','timestamp with time zone'),
        ('agenda','updated_by','bigint'),
        ('alerta','id','bigint'),
        ('alerta','tenant_id','bigint'),
        ('alerta','entidade_id','bigint'),
        ('alerta','fornecedor_id','bigint'),
        ('alerta','documento_id','bigint'),
        ('alerta','agenda_id','bigint'),
        ('alerta','tipo','character varying(40)'),
        ('alerta','mensagem','text'),
        ('alerta','status','character varying(20)'),
        ('alerta','vencimento_at','timestamp with time zone'),
        ('alerta','created_at','timestamp with time zone'),
        ('alerta','created_by','bigint'),
        ('sincronizacao','id','bigint'),
        ('sincronizacao','tenant_id','bigint'),
        ('sincronizacao','entidade_id','bigint'),
        ('sincronizacao','fonte_id','bigint'),
        ('sincronizacao','importacao_id','bigint'),
        ('sincronizacao','status','character varying(25)'),
        ('sincronizacao','tentativa_at','timestamp with time zone'),
        ('sincronizacao','finalizada_at','timestamp with time zone'),
        ('sincronizacao','erro_sanitizado','text'),
        ('sincronizacao','correlation_id','character varying(120)'),
        ('sincronizacao','created_by','bigint'),
        ('auditoria','id','bigint'),
        ('auditoria','tenant_id','bigint'),
        ('auditoria','entidade_id','bigint'),
        ('auditoria','entidade','character varying(80)'),
        ('auditoria','registro_id','bigint'),
        ('auditoria','acao','character varying(60)'),
        ('auditoria','detalhes','jsonb'),
        ('auditoria','usuario_id','bigint'),
        ('auditoria','ocorrido_at','timestamp with time zone'),
        ('auditoria','correlation_id','character varying(120)')
      ) required(table_name,column_name,expected_type)
      join information_schema.columns c on c.table_schema='sigov' and c.table_name='compras_licitapro_'||required.table_name and c.column_name=required.column_name
     where (c.data_type || case when c.data_type='character varying' then '(' || c.character_maximum_length || ')' when c.data_type='numeric' then '(' || c.numeric_precision || ',' || c.numeric_scale || ')' else '' end) <> required.expected_type
     limit 1;
    if found then
        raise exception 'LICITAPRO_COLUMN_TYPE_INCOMPATIBLE: tabela sigov.compras_licitapro_%, coluna %, tipo atual %, esperado %', mismatch.table_name, mismatch.column_name, mismatch.actual_type, mismatch.expected_type;
    end if;
end $$;

alter table sigov.compras_licitapro_fonte add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_fonte add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_fonte add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_fonte add column if not exists nome varchar(160) not null;
alter table sigov.compras_licitapro_fonte add column if not exists tipo varchar(30) not null;
alter table sigov.compras_licitapro_fonte add column if not exists endpoint_url text;
alter table sigov.compras_licitapro_fonte add column if not exists configurada boolean not null default false;
alter table sigov.compras_licitapro_fonte add column if not exists ativa boolean not null default true;
alter table sigov.compras_licitapro_fonte add column if not exists ultima_sincronizacao_at timestamptz;
alter table sigov.compras_licitapro_fonte add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_fonte add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_fonte add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_fonte add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_importacao add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_importacao add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_importacao add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_importacao add column if not exists fonte_id bigint not null;
alter table sigov.compras_licitapro_importacao add column if not exists versao integer not null;
alter table sigov.compras_licitapro_importacao add column if not exists status varchar(25) not null default 'PENDENTE';
alter table sigov.compras_licitapro_importacao add column if not exists iniciada_at timestamptz not null default now();
alter table sigov.compras_licitapro_importacao add column if not exists concluida_at timestamptz;
alter table sigov.compras_licitapro_importacao add column if not exists itens_lidos integer not null default 0;
alter table sigov.compras_licitapro_importacao add column if not exists itens_importados integer not null default 0;
alter table sigov.compras_licitapro_importacao add column if not exists erro_sanitizado text;
alter table sigov.compras_licitapro_importacao add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_oportunidade add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists fonte_id bigint not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists importacao_id bigint;
alter table sigov.compras_licitapro_oportunidade add column if not exists identificador_externo varchar(180) not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists numero varchar(120) not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists objeto text not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists modalidade varchar(100) not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists orgao varchar(200);
alter table sigov.compras_licitapro_oportunidade add column if not exists data_publicacao date not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists data_limite date;
alter table sigov.compras_licitapro_oportunidade add column if not exists status varchar(25) not null default 'ABERTA';
alter table sigov.compras_licitapro_oportunidade add column if not exists url_oficial text;
alter table sigov.compras_licitapro_oportunidade add column if not exists valor_estimado numeric(18,2);
alter table sigov.compras_licitapro_oportunidade add column if not exists processo_id bigint;
alter table sigov.compras_licitapro_oportunidade add column if not exists versao_fonte integer not null default 1;
alter table sigov.compras_licitapro_oportunidade add column if not exists payload_hash varchar(64);
alter table sigov.compras_licitapro_oportunidade add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_oportunidade add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_oportunidade add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_oportunidade add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_documento add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_documento add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_documento add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_documento add column if not exists fornecedor_id bigint not null;
alter table sigov.compras_licitapro_documento add column if not exists tipo varchar(80) not null;
alter table sigov.compras_licitapro_documento add column if not exists titulo varchar(180) not null;
alter table sigov.compras_licitapro_documento add column if not exists validade date;
alter table sigov.compras_licitapro_documento add column if not exists status varchar(25) not null default 'PENDENTE';
alter table sigov.compras_licitapro_documento add column if not exists referencia_documental text;
alter table sigov.compras_licitapro_documento add column if not exists aprovado_por bigint;
alter table sigov.compras_licitapro_documento add column if not exists aprovado_at timestamptz;
alter table sigov.compras_licitapro_documento add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_documento add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_documento add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_documento add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_checklist add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_checklist add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_checklist add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_checklist add column if not exists fornecedor_id bigint not null;
alter table sigov.compras_licitapro_checklist add column if not exists processo_id bigint not null;
alter table sigov.compras_licitapro_checklist add column if not exists status varchar(20) not null default 'PENDENTE';
alter table sigov.compras_licitapro_checklist add column if not exists concluido_at timestamptz;
alter table sigov.compras_licitapro_checklist add column if not exists concluido_por bigint;
alter table sigov.compras_licitapro_checklist add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_checklist add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_checklist add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_checklist add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_checklist_item add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_checklist_item add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_checklist_item add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_checklist_item add column if not exists checklist_id bigint not null;
alter table sigov.compras_licitapro_checklist_item add column if not exists requisito text not null;
alter table sigov.compras_licitapro_checklist_item add column if not exists obrigatorio boolean not null default true;
alter table sigov.compras_licitapro_checklist_item add column if not exists status varchar(20) not null default 'PENDENTE';
alter table sigov.compras_licitapro_checklist_item add column if not exists justificativa text;
alter table sigov.compras_licitapro_checklist_item add column if not exists documento_id bigint;
alter table sigov.compras_licitapro_checklist_item add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_checklist_item add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_checklist_item add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_checklist_item add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_analise add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_analise add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_analise add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_analise add column if not exists processo_id bigint not null;
alter table sigov.compras_licitapro_analise add column if not exists oportunidade_id bigint;
alter table sigov.compras_licitapro_analise add column if not exists status varchar(20) not null default 'RASCUNHO';
alter table sigov.compras_licitapro_analise add column if not exists observacoes text;
alter table sigov.compras_licitapro_analise add column if not exists riscos text;
alter table sigov.compras_licitapro_analise add column if not exists sugestao text;
alter table sigov.compras_licitapro_analise add column if not exists justificativa_responsavel text;
alter table sigov.compras_licitapro_analise add column if not exists responsavel_id bigint not null;
alter table sigov.compras_licitapro_analise add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_analise add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_analise add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_analise add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_criterio add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_criterio add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_criterio add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_criterio add column if not exists analise_id bigint not null;
alter table sigov.compras_licitapro_criterio add column if not exists nome varchar(160) not null;
alter table sigov.compras_licitapro_criterio add column if not exists peso_percentual numeric(5,2) not null;
alter table sigov.compras_licitapro_criterio add column if not exists aderencia_percentual numeric(5,2) not null;
alter table sigov.compras_licitapro_criterio add column if not exists score numeric(12,4) not null;
alter table sigov.compras_licitapro_criterio add column if not exists explicacao text not null;
alter table sigov.compras_licitapro_criterio add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_criterio add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_agenda add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists oportunidade_id bigint;
alter table sigov.compras_licitapro_agenda add column if not exists processo_id bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists fornecedor_id bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists titulo varchar(180) not null;
alter table sigov.compras_licitapro_agenda add column if not exists prazo_at timestamptz not null;
alter table sigov.compras_licitapro_agenda add column if not exists status varchar(25) not null default 'PREPARACAO';
alter table sigov.compras_licitapro_agenda add column if not exists responsavel_id bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists alerta_bloqueio text;
alter table sigov.compras_licitapro_agenda add column if not exists contrato_id bigint;
alter table sigov.compras_licitapro_agenda add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_agenda add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_agenda add column if not exists updated_at timestamptz;
alter table sigov.compras_licitapro_agenda add column if not exists updated_by bigint;
alter table sigov.compras_licitapro_alerta add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_alerta add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_alerta add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_alerta add column if not exists fornecedor_id bigint;
alter table sigov.compras_licitapro_alerta add column if not exists documento_id bigint;
alter table sigov.compras_licitapro_alerta add column if not exists agenda_id bigint;
alter table sigov.compras_licitapro_alerta add column if not exists tipo varchar(40) not null;
alter table sigov.compras_licitapro_alerta add column if not exists mensagem text not null;
alter table sigov.compras_licitapro_alerta add column if not exists status varchar(20) not null default 'ABERTO';
alter table sigov.compras_licitapro_alerta add column if not exists vencimento_at timestamptz;
alter table sigov.compras_licitapro_alerta add column if not exists created_at timestamptz not null default now();
alter table sigov.compras_licitapro_alerta add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_sincronizacao add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_sincronizacao add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_sincronizacao add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_sincronizacao add column if not exists fonte_id bigint not null;
alter table sigov.compras_licitapro_sincronizacao add column if not exists importacao_id bigint;
alter table sigov.compras_licitapro_sincronizacao add column if not exists status varchar(25) not null;
alter table sigov.compras_licitapro_sincronizacao add column if not exists tentativa_at timestamptz not null default now();
alter table sigov.compras_licitapro_sincronizacao add column if not exists finalizada_at timestamptz;
alter table sigov.compras_licitapro_sincronizacao add column if not exists erro_sanitizado text;
alter table sigov.compras_licitapro_sincronizacao add column if not exists correlation_id varchar(120);
alter table sigov.compras_licitapro_sincronizacao add column if not exists created_by bigint not null;
alter table sigov.compras_licitapro_auditoria add column if not exists id bigint generated always as identity;
alter table sigov.compras_licitapro_auditoria add column if not exists tenant_id bigint not null;
alter table sigov.compras_licitapro_auditoria add column if not exists entidade_id bigint not null;
alter table sigov.compras_licitapro_auditoria add column if not exists entidade varchar(80) not null;
alter table sigov.compras_licitapro_auditoria add column if not exists registro_id bigint;
alter table sigov.compras_licitapro_auditoria add column if not exists acao varchar(60) not null;
alter table sigov.compras_licitapro_auditoria add column if not exists detalhes jsonb;
alter table sigov.compras_licitapro_auditoria add column if not exists usuario_id bigint not null;
alter table sigov.compras_licitapro_auditoria add column if not exists ocorrido_at timestamptz not null default now();
alter table sigov.compras_licitapro_auditoria add column if not exists correlation_id varchar(120);

do $$
declare fk record;
begin
  for fk in select * from (values
    ('compras_licitapro_importacao','fonte_id','compras_licitapro_fonte','id','fk_clp_importacao_fonte_id'),
    ('compras_licitapro_oportunidade','fonte_id','compras_licitapro_fonte','id','fk_clp_oportunidade_fonte_id'),
    ('compras_licitapro_oportunidade','importacao_id','compras_licitapro_importacao','id','fk_clp_oportunidade_importacao_id'),
    ('compras_licitapro_oportunidade','processo_id','compras_processo','id','fk_clp_oportunidade_processo_id'),
    ('compras_licitapro_documento','fornecedor_id','compras_fornecedor','id','fk_clp_documento_fornecedor_id'),
    ('compras_licitapro_checklist','fornecedor_id','compras_fornecedor','id','fk_clp_checklist_fornecedor_id'),
    ('compras_licitapro_checklist','processo_id','compras_processo','id','fk_clp_checklist_processo_id'),
    ('compras_licitapro_checklist_item','checklist_id','compras_licitapro_checklist','id','fk_clp_checklist_item_checklist_id'),
    ('compras_licitapro_checklist_item','documento_id','compras_licitapro_documento','id','fk_clp_checklist_item_documento_id'),
    ('compras_licitapro_analise','processo_id','compras_processo','id','fk_clp_analise_processo_id'),
    ('compras_licitapro_analise','oportunidade_id','compras_licitapro_oportunidade','id','fk_clp_analise_oportunidade_id'),
    ('compras_licitapro_criterio','analise_id','compras_licitapro_analise','id','fk_clp_criterio_analise_id'),
    ('compras_licitapro_agenda','oportunidade_id','compras_licitapro_oportunidade','id','fk_clp_agenda_oportunidade_id'),
    ('compras_licitapro_agenda','processo_id','compras_processo','id','fk_clp_agenda_processo_id'),
    ('compras_licitapro_agenda','fornecedor_id','compras_fornecedor','id','fk_clp_agenda_fornecedor_id'),
    ('compras_licitapro_agenda','contrato_id','compras_contrato','id','fk_clp_agenda_contrato_id'),
    ('compras_licitapro_alerta','fornecedor_id','compras_fornecedor','id','fk_clp_alerta_fornecedor_id'),
    ('compras_licitapro_alerta','documento_id','compras_licitapro_documento','id','fk_clp_alerta_documento_id'),
    ('compras_licitapro_alerta','agenda_id','compras_licitapro_agenda','id','fk_clp_alerta_agenda_id'),
    ('compras_licitapro_sincronizacao','fonte_id','compras_licitapro_fonte','id','fk_clp_sincronizacao_fonte_id'),
    ('compras_licitapro_sincronizacao','importacao_id','compras_licitapro_importacao','id','fk_clp_sincronizacao_importacao_id')
) v(table_name,column_name,target_table,target_column,constraint_name) loop
    if not exists (select 1 from pg_constraint con join pg_attribute a on a.attrelid=con.conrelid and a.attnum=any(con.conkey) where con.conrelid=to_regclass('sigov.'||fk.table_name) and con.confrelid=to_regclass('sigov.'||fk.target_table) and con.contype='f' and a.attname=fk.column_name) then
      execute format('alter table sigov.%I add constraint %I foreign key (%I) references sigov.%I(%I) not valid',fk.table_name,fk.constraint_name,fk.column_name,fk.target_table,fk.target_column);
    end if;
  end loop;
end $$;

-- Reaplicar integridade, função, triggers e índices canônicos de modo idempotente.
-- CORR03 - fechamento do LicitaPro IA no FUNC03.
-- Migration corretiva aditiva: a EXP03 publicada permanece imutável.

do $$
begin
    if not exists (select 1 from pg_constraint where conrelid=to_regclass('sigov.compras_licitapro_fonte') and conname = 'ck_clp_fonte_endpoint_url') then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid=to_regclass('sigov.compras_licitapro_documento') and conname = 'ck_clp_doc_referencia_preenchida') then
        alter table sigov.compras_licitapro_documento
            add constraint ck_clp_doc_referencia_preenchida
            check (status <> 'APROVADO' or nullif(btrim(referencia_documental), '') is not null) not valid;
    end if;
    if not exists (select 1 from pg_constraint where conrelid=to_regclass('sigov.compras_licitapro_criterio') and conname = 'ck_clp_criterio_explicacao_preenchida') then
        alter table sigov.compras_licitapro_criterio
            add constraint ck_clp_criterio_explicacao_preenchida
            check (nullif(btrim(explicacao), '') is not null) not valid;
    end if;
end $$;

create index if not exists ix_clp_importacao_tenant_fonte
    on sigov.compras_licitapro_importacao(tenant_id, entidade_id, fonte_id, iniciada_at desc);
create index if not exists ix_clp_item_tenant_checklist_status
    on sigov.compras_licitapro_checklist_item(tenant_id, entidade_id, checklist_id, status);
create index if not exists ix_clp_analise_tenant_processo
    on sigov.compras_licitapro_analise(tenant_id, entidade_id, processo_id, status);
create index if not exists ix_clp_criterio_tenant_analise
    on sigov.compras_licitapro_criterio(tenant_id, entidade_id, analise_id);
create index if not exists ix_clp_alerta_tenant_status_vencimento
    on sigov.compras_licitapro_alerta(tenant_id, entidade_id, status, vencimento_at);

create or replace function sigov.compras_licitapro_validar_relacoes()
returns trigger language plpgsql as $$
begin
    if tg_table_name = 'compras_licitapro_checklist_item' and not exists (
        select 1 from sigov.compras_licitapro_checklist x where x.id=new.checklist_id and x.tenant_id=new.tenant_id and x.entidade_id=new.entidade_id
    ) then raise exception 'Checklist fora do contexto tenant/entidade';
    elsif tg_table_name = 'compras_licitapro_criterio' and not exists (
        select 1 from sigov.compras_licitapro_analise x where x.id=new.analise_id and x.tenant_id=new.tenant_id and x.entidade_id=new.entidade_id
    ) then raise exception 'Análise fora do contexto tenant/entidade';
    end if;
    return new;
end $$;

drop trigger if exists trg_clp_checklist_item_contexto on sigov.compras_licitapro_checklist_item;
create trigger trg_clp_checklist_item_contexto before insert or update on sigov.compras_licitapro_checklist_item
for each row execute function sigov.compras_licitapro_validar_relacoes();
drop trigger if exists trg_clp_criterio_contexto on sigov.compras_licitapro_criterio;
create trigger trg_clp_criterio_contexto before insert or update on sigov.compras_licitapro_criterio
for each row execute function sigov.compras_licitapro_validar_relacoes();


-- Completar chaves primárias e constraints de domínio em relações parciais.
do $$
declare table_name text;
begin
    foreach table_name in array array[
        'compras_licitapro_fonte','compras_licitapro_importacao','compras_licitapro_oportunidade',
        'compras_licitapro_documento','compras_licitapro_checklist','compras_licitapro_checklist_item',
        'compras_licitapro_analise','compras_licitapro_criterio','compras_licitapro_agenda',
        'compras_licitapro_alerta','compras_licitapro_sincronizacao','compras_licitapro_auditoria'
    ] loop
        if not exists (select 1 from pg_constraint c where c.conrelid=to_regclass('sigov.'||table_name) and c.contype='p') then
            execute format('alter table sigov.%I add constraint %I primary key (id)', table_name, 'pk_'||table_name);
        end if;
    end loop;
end $$;

do $constraints$
declare item record;
begin
    for item in select * from (values
      ('compras_licitapro_fonte','ck_clp_fonte_tipo',             $$tipo in('PNCP','PORTAL_PUBLICO','OUTRA_OFICIAL')$$),
      ('compras_licitapro_fonte','ck_clp_fonte_config',           $$not configurada or endpoint_url is not null$$),
      ('compras_licitapro_importacao','ck_clp_import_status',     $$status in('PENDENTE','PROCESSANDO','CONCLUIDA','FALHA','INDISPONIVEL')$$),
      ('compras_licitapro_importacao','ck_clp_import_qtd',        $$itens_lidos>=0 and itens_importados>=0 and itens_importados<=itens_lidos$$),
      ('compras_licitapro_oportunidade','ck_clp_oport_status',    $$status in('ABERTA','VINCULADA','VENCIDA','CANCELADA','INDISPONIVEL')$$),
      ('compras_licitapro_oportunidade','ck_clp_oport_datas',     $$data_limite is null or data_limite>=data_publicacao$$),
      ('compras_licitapro_oportunidade','ck_clp_oport_valor',     $$valor_estimado is null or valor_estimado>=0$$),
      ('compras_licitapro_documento','ck_clp_doc_status',         $$status in('PENDENTE','EM_ANALISE','APROVADO','REPROVADO','VENCIDO')$$),
      ('compras_licitapro_documento','ck_clp_doc_aprovado',       $$status<>'APROVADO' or (validade is not null and referencia_documental is not null)$$),
      ('compras_licitapro_checklist','ck_clp_check_status',       $$status in('PENDENTE','EM_ANALISE','CONCLUIDO','BLOQUEADO')$$),
      ('compras_licitapro_checklist_item','ck_clp_item_status',   $$status in('PENDENTE','ATENDIDO','NAO_APLICAVEL','BLOQUEADO')$$),
      ('compras_licitapro_checklist_item','ck_clp_item_just',     $$not(obrigatorio and status in('NAO_APLICAVEL','BLOQUEADO')) or justificativa is not null$$),
      ('compras_licitapro_analise','ck_clp_analise_status',       $$status in('RASCUNHO','EM_REVISAO','CONCLUIDA')$$),
      ('compras_licitapro_criterio','ck_clp_criterio_percent',    $$peso_percentual between 0 and 100 and aderencia_percentual between 0 and 100$$),
      ('compras_licitapro_criterio','ck_clp_criterio_score',      $$score>=0$$),
      ('compras_licitapro_agenda','ck_clp_agenda_status',         $$status in('PREPARACAO','PRONTA','ENVIADA','VENCIDA','CANCELADA','CONQUISTADA')$$),
      ('compras_licitapro_alerta','ck_clp_alerta_status',         $$status in('ABERTO','CIENTE','RESOLVIDO')$$),
      ('compras_licitapro_sincronizacao','ck_clp_sync_status',    $$status in('PROCESSANDO','CONCLUIDA','FALHA','INDISPONIVEL','NAO_CONFIGURADA')$$)
    ) v(table_name,constraint_name,definition) loop
        if not exists (select 1 from pg_constraint c where c.conrelid=to_regclass('sigov.'||item.table_name) and c.conname=item.constraint_name and c.contype='c') then
            execute format('alter table sigov.%I add constraint %I check (%s) not valid',item.table_name,item.constraint_name,item.definition);
        end if;
    end loop;
end $constraints$;

-- A consulta é deliberadamente vinculada ao conrelid correto.
do $$
begin
    if not exists (
        select 1 from pg_constraint c
        where c.conrelid = to_regclass('sigov.compras_licitapro_fonte')
          and c.conname = 'ck_clp_fonte_endpoint_url'
    ) then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    end if;
end $$;

-- Criação protegida para preservar compatibilidade com validadores históricos.
do $$
begin
    if to_regclass('sigov.ix_clp_alerta_tenant_status_vencimento') is null then
        create index ix_clp_alerta_tenant_status_vencimento
            on sigov.compras_licitapro_alerta (tenant_id, entidade_id, status, vencimento_at);
    end if;
end $$;

-- PostgreSQL cria este índice no mesmo schema da tabela.
create index if not exists ix_clp_alerta_tenant_status_vencimento
on sigov.compras_licitapro_alerta
   (tenant_id, entidade_id, status, vencimento_at);
