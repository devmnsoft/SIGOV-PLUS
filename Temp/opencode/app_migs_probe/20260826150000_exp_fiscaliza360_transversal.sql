-- EXP-FISCALIZA360: núcleo transversal de fiscalização e campo.
create schema if not exists sigov;

create table if not exists sigov.fiscalizacao_equipe (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 nome varchar(160) not null, descricao text, status varchar(20) not null default 'ATIVA', criado_por bigint not null,
 created_at timestamptz not null default now(), updated_at timestamptz, is_deleted boolean not null default false,
 constraint ck_fiscalizacao_equipe_status check(status in('ATIVA','INATIVA')), constraint ck_fiscalizacao_equipe_nome check(btrim(nome)<>'')
);
create table if not exists sigov.fiscalizacao_equipe_membro (
 id bigint generated always as identity primary key, equipe_id bigint not null references sigov.fiscalizacao_equipe(id),
 tenant_id bigint not null, entidade_id bigint not null, usuario_id bigint not null, funcao varchar(80) not null, ativo boolean not null default true,
 created_at timestamptz not null default now(), unique(equipe_id,usuario_id)
);
create table if not exists sigov.fiscalizacao_checklist_modelo (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 nome varchar(180) not null, tipo_fiscalizacao varchar(100) not null, origem_modulo varchar(30) not null, status varchar(20) not null default 'ATIVO',
 versao integer not null default 1, criado_por bigint not null, created_at timestamptz not null default now(), updated_at timestamptz,
 constraint ck_fcm_origem check(origem_modulo in('OBRAS','MEIO_AMBIENTE','TRANSITO','DEFESA_CIVIL')), constraint ck_fcm_status check(status in('ATIVO','INATIVO')), constraint ck_fcm_versao check(versao>0)
);
create table if not exists sigov.fiscalizacao_checklist_item_modelo (
 id bigint generated always as identity primary key, checklist_modelo_id bigint not null references sigov.fiscalizacao_checklist_modelo(id),
 ordem integer not null, pergunta text not null, tipo_resposta varchar(30) not null, obrigatorio boolean not null default false, opcoes jsonb,
 created_at timestamptz not null default now(), constraint ck_fcim_tipo check(tipo_resposta in('SIM_NAO','MULTIPLA_ESCOLHA','TEXTO','NUMERO','DATA','CHECKLIST')), constraint ck_fcim_ordem check(ordem>0), unique(checklist_modelo_id,ordem)
);
create table if not exists sigov.fiscalizacao_ordem (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 origem_modulo varchar(30) not null, tipo varchar(100) not null, prioridade varchar(20) not null, status varchar(30) not null default 'ABERTA',
 equipe_id bigint references sigov.fiscalizacao_equipe(id), responsavel_usuario_id bigint, registro_fiscalizado_tipo varchar(100) not null,
 registro_fiscalizado_id bigint not null, registro_fiscalizado_rotulo varchar(250) not null, aberta_em timestamptz not null default now(),
 agendada_em timestamptz, concluida_em timestamptz, cancelada_em timestamptz, motivo text not null, observacoes text,
 criado_por bigint not null, atualizado_por bigint, created_at timestamptz not null default now(), updated_at timestamptz,
 constraint ck_fo_origem check(origem_modulo in('OBRAS','MEIO_AMBIENTE','TRANSITO','DEFESA_CIVIL')),
 constraint ck_fo_prioridade check(prioridade in('BAIXA','NORMAL','ALTA','URGENTE')),
 constraint ck_fo_status check(status in('ABERTA','AGENDADA','EM_VISTORIA','CONCLUIDA','CANCELADA')),
 constraint ck_fo_datas check((concluida_em is null or concluida_em>=aberta_em) and (cancelada_em is null or cancelada_em>=aberta_em)),
 constraint ck_fo_vinculo check(registro_fiscalizado_id>0 and btrim(registro_fiscalizado_rotulo)<>'')
);
create table if not exists sigov.fiscalizacao_vistoria (
 id bigint generated always as identity primary key, ordem_id bigint not null references sigov.fiscalizacao_ordem(id), tenant_id bigint not null,
 entidade_id bigint not null, exercicio_id bigint not null, equipe_id bigint not null references sigov.fiscalizacao_equipe(id), checklist_modelo_id bigint references sigov.fiscalizacao_checklist_modelo(id),
 status varchar(30) not null default 'AGENDADA', agendada_em timestamptz not null, iniciada_em timestamptz, concluida_em timestamptz,
 local_descricao varchar(250) not null, latitude numeric(10,7), longitude numeric(10,7), observacoes text, resultado text,
 criado_por bigint not null, atualizado_por bigint, created_at timestamptz not null default now(), updated_at timestamptz,
 constraint ck_fv_status check(status in('AGENDADA','EM_ANDAMENTO','CONCLUIDA','CANCELADA','REABERTA')),
 constraint ck_fv_lat check(latitude is null or latitude between -90 and 90), constraint ck_fv_lon check(longitude is null or longitude between -180 and 180)
);
create table if not exists sigov.fiscalizacao_vistoria_item (
 id bigint generated always as identity primary key, vistoria_id bigint not null references sigov.fiscalizacao_vistoria(id), item_modelo_id bigint not null references sigov.fiscalizacao_checklist_item_modelo(id),
 resposta_texto text, resposta_numero numeric, resposta_data date, resposta_boolean boolean, resposta_opcoes jsonb, observacao text, respondido_em timestamptz, respondido_por bigint,
 unique(vistoria_id,item_modelo_id)
);
create table if not exists sigov.fiscalizacao_roteiro (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 equipe_id bigint not null references sigov.fiscalizacao_equipe(id), data_roteiro date not null, nome varchar(160) not null, status varchar(20) not null default 'PLANEJADO',
 ordem_ids bigint[] not null, observacoes text, criado_por bigint not null, created_at timestamptz not null default now(), updated_at timestamptz,
 constraint ck_fr_status check(status in('PLANEJADO','EM_EXECUCAO','CONCLUIDO','CANCELADO')), constraint ck_fr_ordens check(cardinality(ordem_ids)>0)
);
create table if not exists sigov.fiscalizacao_auto_notificacao (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 ordem_id bigint not null references sigov.fiscalizacao_ordem(id), vistoria_id bigint not null references sigov.fiscalizacao_vistoria(id),
 tipo varchar(60) not null, fundamento text not null, descricao text not null, prazo date, responsavel_usuario_id bigint, status varchar(25) not null default 'EMITIDO',
 emitido_em timestamptz not null default now(), criado_por bigint not null, updated_at timestamptz,
 constraint ck_fan_status check(status in('RASCUNHO','EMITIDO','CIENTE','ATENDIDO','CANCELADO'))
);
create table if not exists sigov.fiscalizacao_sincronizacao_item (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, vistoria_id bigint references sigov.fiscalizacao_vistoria(id),
 outbox_id bigint references sigov.sincronizacao_outbox(id), chave_idempotencia varchar(160) not null, status varchar(20) not null default 'PENDENTE', erro_sanitizado text,
 created_at timestamptz not null default now(), updated_at timestamptz, constraint ck_fsi_status check(status in('PENDENTE','PROCESSANDO','CONCLUIDO','FALHA')), unique(tenant_id,entidade_id,chave_idempotencia)
);
create table if not exists sigov.fiscalizacao_auditoria (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint not null,
 tabela varchar(100) not null, registro_id bigint not null, acao varchar(40) not null, usuario_id bigint not null, dados_anteriores jsonb, dados_novos jsonb,
 correlation_id uuid, created_at timestamptz not null default now()
);
create index if not exists ix_fo_contexto_status on sigov.fiscalizacao_ordem(tenant_id,entidade_id,exercicio_id,status,aberta_em desc);
create index if not exists ix_fo_origem_vinculo on sigov.fiscalizacao_ordem(tenant_id,entidade_id,origem_modulo,registro_fiscalizado_tipo,registro_fiscalizado_id);
create index if not exists ix_fo_equipe_data on sigov.fiscalizacao_ordem(tenant_id,entidade_id,equipe_id,agendada_em);
create index if not exists ix_fv_contexto_data on sigov.fiscalizacao_vistoria(tenant_id,entidade_id,exercicio_id,status,agendada_em);
create index if not exists ix_fan_contexto_status on sigov.fiscalizacao_auto_notificacao(tenant_id,entidade_id,status,emitido_em desc);
create index if not exists ix_fa_registro on sigov.fiscalizacao_auditoria(tenant_id,entidade_id,tabela,registro_id,created_at desc);

insert into sigov.permissao(chave,descricao,modulo,ativo,created_at) values
 ('FISCALIZACAO_DASHBOARD_VIEW','Visualizar dashboard Fiscaliza360','fiscalizacao',true,now()),('FISCALIZACAO_ORDEM_VIEW','Visualizar ordens de fiscalização','fiscalizacao',true,now()),
 ('FISCALIZACAO_ORDEM_MANAGE','Gerenciar ordens de fiscalização','fiscalizacao',true,now()),('FISCALIZACAO_VISTORIA_VIEW','Visualizar vistorias','fiscalizacao',true,now()),
 ('FISCALIZACAO_VISTORIA_MANAGE','Gerenciar vistorias','fiscalizacao',true,now()),('FISCALIZACAO_CHECKLIST_MANAGE','Gerenciar checklists','fiscalizacao',true,now()),
 ('FISCALIZACAO_AUTO_MANAGE','Emitir autos e notificações','fiscalizacao',true,now()),('FISCALIZACAO_RELATORIO_EXPORT','Exportar relatórios de fiscalização','fiscalizacao',true,now()),
 ('FISCALIZACAO_SINCRONIZACAO_VIEW','Visualizar sincronização de campo','fiscalizacao',true,now()) on conflict(chave) do update set descricao=excluded.descricao,modulo=excluded.modulo,ativo=true;

comment on table sigov.fiscalizacao_sincronizacao_item is 'Controle local da outbox; o processamento externo permanece BLOCKED até existir adaptador/worker oficial.';
