-- Jornada autorizada entre Secretaria Escolar e responsáveis.
-- O público do comunicado é fotografado em educacao_comunicado_destinatario na publicação;
-- a autorização vigente do vínculo continua obrigatória em toda leitura e ciência.
alter table sigov.educacao_comunicado
    add column if not exists versao integer not null default 1,
    add column if not exists exige_ciencia boolean not null default false,
    add column if not exists disponivel_de timestamptz,
    add column if not exists disponivel_ate timestamptz;

alter table sigov.educacao_comunicado_destinatario
    add column if not exists versao integer not null default 1,
    add column if not exists ciencia_at timestamptz;

create index if not exists ix_educacao_portal_vinculo_autorizacao
    on sigov.educacao_portal_vinculo(tenant_id, usuario_id, aluno_id, status)
    where is_deleted=false;
create index if not exists ix_educacao_comunicado_destinatario_acesso
    on sigov.educacao_comunicado_destinatario(tenant_id, usuario_id, aluno_id, comunicado_id, versao);

comment on column sigov.educacao_comunicado_destinatario.lido_at is
    'Abertura registrada separadamente; nunca representa ciência.';
comment on column sigov.educacao_comunicado_destinatario.ciencia_at is
    'Ação explícita e idempotente de ciência; não representa assinatura ou concordância jurídica.';
