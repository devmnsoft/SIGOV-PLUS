-- RC50.63: núcleo persistente das centrais transversais.
set search_path to sigov;
create table if not exists sigov.pendencia_operacional (
    id bigserial primary key, tenant_id bigint not null, modulo varchar(80) not null,
    recurso varchar(120) not null, tipo varchar(120) not null, entidade varchar(120) not null,
    entidade_id varchar(120) not null, gravidade varchar(20) not null,
    titulo varchar(200) not null, descricao varchar(1000), prazo timestamptz,
    responsavel_usuario_id bigint, rota_acao varchar(300) not null, status varchar(30) not null default 'ABERTA',
    created_at timestamptz not null default now(), resolved_at timestamptz,
    constraint ck_pendencia_gravidade check (gravidade in ('CRITICA','ALTA','MEDIA','BAIXA','INFORMATIVA')),
    constraint ck_pendencia_status check (status in ('ABERTA','EM_TRATAMENTO','RESOLVIDA','CANCELADA'))
);
create unique index if not exists ux_pendencia_operacional_aberta
    on sigov.pendencia_operacional (tenant_id, modulo, tipo, entidade, entidade_id)
    where status in ('ABERTA','EM_TRATAMENTO');
create index if not exists ix_pendencia_operacional_tenant_status
    on sigov.pendencia_operacional (tenant_id, status, gravidade, prazo);

create table if not exists sigov.alerta_operacional (
    id bigserial primary key, tenant_id bigint not null, modulo varchar(80) not null,
    tipo varchar(40) not null, severidade varchar(20) not null, titulo varchar(200) not null,
    descricao varchar(1000), rota_acao varchar(300) not null, status varchar(30) not null default 'ATIVO',
    entidade varchar(120), entidade_id varchar(120), created_at timestamptz not null default now(),
    resolved_at timestamptz, resolved_by bigint, justificativa varchar(1000),
    constraint ck_alerta_status check (status in ('ATIVO','SILENCIADO','RESOLVIDO')),
    constraint ck_alerta_severidade check (severidade in ('CRITICA','ALTA','MEDIA','BAIXA','INFORMATIVA'))
);
alter table sigov.alerta_operacional add column if not exists rota_acao varchar(300);
alter table sigov.alerta_operacional add column if not exists justificativa varchar(1000);
update sigov.alerta_operacional set rota_acao='/Alertas' where rota_acao is null;
create index if not exists ix_alerta_operacional_tenant_status
    on sigov.alerta_operacional (tenant_id, status, severidade, created_at desc);

create table if not exists sigov.qualidade_dados_ocorrencia (
    id bigserial primary key, tenant_id bigint not null, modulo varchar(80) not null,
    regra varchar(160) not null, entidade varchar(120) not null, entidade_id varchar(120) not null,
    severidade varchar(20) not null, descricao varchar(1000) not null, rota_correcao varchar(300),
    status varchar(30) not null default 'ABERTA', detected_at timestamptz not null default now(), resolved_at timestamptz,
    constraint ck_qualidade_status check (status in ('ABERTA','EM_CORRECAO','RESOLVIDA','ACEITA'))
);
create unique index if not exists ux_qualidade_dados_aberta
    on sigov.qualidade_dados_ocorrencia (tenant_id, modulo, regra, entidade, entidade_id)
    where status in ('ABERTA','EM_CORRECAO');

create table if not exists sigov.integracao_interna_evento (
    id bigserial primary key, tenant_id bigint not null, origem varchar(80) not null,
    destino varchar(80) not null, status varchar(40) not null, tipo_evento varchar(120) not null,
    referencia varchar(160), detalhe_erro varchar(1000), rota_correcao varchar(300),
    preparatoria boolean not null default false, created_at timestamptz not null default now(),
    constraint ck_integracao_interna_status check (status in ('ATIVA','PREPARATORIA','PENDENTE_CONFIGURACAO','COM_ERRO','DESABILITADA'))
);
create index if not exists ix_integracao_interna_tenant_data
    on sigov.integracao_interna_evento (tenant_id, created_at desc);

insert into sigov.permissao (chave, modulo, descricao)
values
 ('governanca.pendencias.visualizar','governanca','Visualizar pendências transversais'),
 ('governanca.alertas.visualizar','governanca','Visualizar alertas transversais'),
 ('governanca.alertas.resolver','governanca','Resolver alertas transversais'),
 ('governanca.qualidade.visualizar','governanca','Visualizar qualidade de dados'),
 ('governanca.integracoes.visualizar','governanca','Visualizar integrações internas'),
 ('governanca.status_funcional.visualizar','governanca','Visualizar status funcional')
on conflict (chave) do update set descricao=excluded.descricao;
