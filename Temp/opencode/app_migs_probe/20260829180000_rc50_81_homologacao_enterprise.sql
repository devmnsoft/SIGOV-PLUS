-- RC50.81 - governança de homologação enterprise, segurança e operação SaaS.
-- Estruturas idempotentes, isoladas por tenant e sem dados demonstrativos.

create table if not exists sigov.homologacao_item (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint,
    unidade_id bigint,
    exercicio_id bigint,
    modulo varchar(80) not null,
    titulo varchar(200) not null,
    descricao text,
    status varchar(20) not null default 'PENDENTE',
    severidade varchar(10),
    responsavel_usuario_id bigint,
    evidencia text,
    ged_documento_id bigint,
    criado_por bigint not null,
    criado_em timestamptz not null default now(),
    atualizado_por bigint,
    atualizado_em timestamptz,
    ativo boolean not null default true,
    constraint ck_homologacao_item_status check (status in ('PENDENTE','EM_VALIDACAO','APROVADO','REPROVADO','BLOQUEADO')),
    constraint ck_homologacao_item_severidade check (severidade is null or severidade in ('BAIXA','MEDIA','ALTA','CRITICA'))
);

create table if not exists sigov.homologacao_historico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    item_id bigint not null references sigov.homologacao_item(id),
    status_anterior varchar(20),
    status_novo varchar(20) not null,
    justificativa text not null,
    usuario_id bigint not null,
    criado_em timestamptz not null default now()
);

create table if not exists sigov.operacao_evento_auditoria (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint,
    categoria varchar(40) not null,
    operacao varchar(100) not null,
    recurso varchar(160),
    resultado varchar(20) not null,
    duracao_ms integer,
    usuario_id bigint,
    justificativa text,
    correlation_id uuid,
    criado_em timestamptz not null default now(),
    constraint ck_operacao_evento_resultado check (resultado in ('SUCESSO','FALHA','BLOQUEADO','PENDENTE')),
    constraint ck_operacao_evento_duracao check (duracao_ms is null or duracao_ms >= 0)
);

create index if not exists ix_homologacao_item_painel
    on sigov.homologacao_item (tenant_id, status, severidade, modulo) where ativo;
create index if not exists ix_homologacao_item_contexto
    on sigov.homologacao_item (tenant_id, entidade_id, exercicio_id, unidade_id) where ativo;
create index if not exists ix_homologacao_historico_item
    on sigov.homologacao_historico (tenant_id, item_id, criado_em desc);
create index if not exists ix_operacao_evento_consulta
    on sigov.operacao_evento_auditoria (tenant_id, categoria, criado_em desc);

insert into sigov.permissao (chave, descricao, modulo, ativo, created_at)
select p.chave, p.nome, p.modulo, true, now()
from (values
 ('HOMOLOGACAO_DASHBOARD_VIEW','Homologação: visualizar dashboard','homologacao'),
 ('HOMOLOGACAO_CHECKLIST_MANAGE','Homologação: gerenciar checklist','homologacao'),
 ('HOMOLOGACAO_RELATORIO_EXPORT','Homologação: exportar relatório','homologacao'),
 ('SAAS_TENANT_VIEW','SaaS: visualizar tenants','saas'),
 ('SAAS_TENANT_MANAGE','SaaS: gerenciar tenants','saas'),
 ('SAAS_PARAMETRO_VIEW','SaaS: visualizar parâmetros','saas'),
 ('SAAS_PARAMETRO_MANAGE','SaaS: gerenciar parâmetros','saas'),
 ('SAAS_MODULO_MANAGE','SaaS: gerenciar módulos','saas'),
 ('SEGURANCA_AUDITORIA_VIEW','Segurança: visualizar auditoria','seguranca'),
 ('SEGURANCA_EXPORTACAO_VIEW','Segurança: visualizar exportações','seguranca'),
 ('SEGURANCA_ACESSO_SENSIVEL_VIEW','Segurança: visualizar acessos sensíveis','seguranca'),
 ('OPERACAO_SAUDE_VIEW','Operação: visualizar saúde','operacao'),
 ('OPERACAO_PERFORMANCE_VIEW','Operação: visualizar performance','operacao'),
 ('OPERACAO_LOG_VIEW','Operação: visualizar logs sanitizados','operacao'),
 ('OPERACAO_ERRO_MANAGE','Operação: tratar erros','operacao'),
 ('DESIGN_SYSTEM_VIEW','Design system: visualizar catálogo','design_system'),
 ('RELATORIO_CENTRAL_VIEW','Relatórios: visualizar central','relatorios'),
 ('RELATORIO_CENTRAL_EXPORT','Relatórios: exportar pela central','relatorios')
) as p(chave,nome,modulo)
on conflict (chave) do update set descricao=excluded.descricao, modulo=excluded.modulo, ativo=true;
