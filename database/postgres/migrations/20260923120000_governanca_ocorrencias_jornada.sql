-- Jornada transversal autorizada: atribuição, concorrência, revalidação e histórico.
set search_path to sigov;

alter table sigov.pendencia_operacional
  add column if not exists versao bigint not null default 1,
  add column if not exists updated_at timestamptz;

alter table sigov.qualidade_dados_ocorrencia
  add column if not exists responsavel_usuario_id bigint,
  add column if not exists versao bigint not null default 1,
  add column if not exists ultimo_resultado varchar(40),
  add column if not exists verificado_em timestamptz,
  add column if not exists origem_verificada_em timestamptz,
  add column if not exists condicao_presente boolean,
  add column if not exists updated_at timestamptz;

create table if not exists sigov.governanca_ocorrencia_historico (
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  ocorrencia_tipo varchar(20) not null,
  ocorrencia_id bigint not null,
  evento varchar(40) not null,
  usuario_id bigint,
  justificativa varchar(1000),
  dados_antes jsonb,
  dados_depois jsonb,
  ocorrido_em timestamptz not null default now(),
  constraint ck_governanca_ocorrencia_tipo check (ocorrencia_tipo in ('PENDENCIA','QUALIDADE'))
);
create index if not exists ix_governanca_ocorrencia_historico_timeline
  on sigov.governanca_ocorrencia_historico(tenant_id, ocorrencia_tipo, ocorrencia_id, ocorrido_em desc, id desc);
create index if not exists ix_governanca_pendencia_responsavel
  on sigov.pendencia_operacional(tenant_id,responsavel_usuario_id,status);
create index if not exists ix_governanca_qualidade_responsavel
  on sigov.qualidade_dados_ocorrencia(tenant_id,responsavel_usuario_id,status);

insert into sigov.permissao(chave,modulo,descricao)
values ('governanca.ocorrencias.atribuir','governanca','Atribuir e redistribuir ocorrências transversais'),
       ('governanca.qualidade.revalidar','governanca','Solicitar revalidação de qualidade com resultado da origem')
on conflict(chave) do update set descricao=excluded.descricao;
