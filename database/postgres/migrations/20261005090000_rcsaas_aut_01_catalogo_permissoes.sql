-- ============================================================
-- RC-SAAS-AUT | 20261005090000
-- Catalogo de permissoes da administracao SaaS (MNSOFT) e da
-- gestao delegada do cliente.
--
-- Regra 8: correcao por migration nova; nenhuma migration
-- publicada e alterada (a derivacao das chaves saas.superadmin.*
-- causada pela regra %admin% da migration 013 e corrigida aqui).
--
-- Conteudo (ordem proposital):
--   1. Coluna permissao.delegavel (fonte de autoridade: banco).
--   2. Chaves novas do modulo saas (plataforma MNSOFT) e do
--      modulo cliente (operacoes delegaveis ao cliente).
--   3. Normalizacao corretiva das 3 chaves saas.superadmin.*
--      (recurso/acao compativeis com o matching do avaliador
--      persistente, mesma convencao de chave pontuada de
--      saas.tenants.*).
--   4. Bulk delegavel: ativo e fora de (saas, contexto) => true.
--   5. Re-assercao sistemico dos perfis de plataforma
--      (8 codigos do 68b + ADMINISTRADOR_GERAL; template gerido
--      pela plataforma, indisponivel para edicao/clonagem do tenant).
--   6. Grants PERMITIR para copias ativas dos perfis de
--      plataforma/cliente. Justificativa LEGACY_ALIAS para
--      ADMINISTRADOR_GERAL (paridade de transicao auditada);
--      nenhum alias cria autoridade nova.
--   7. Tabela saas_alias_revisao + backfill dos usuarios que hoje
--      possuem perfil com codigo no conjunto de aliases de admin
--      global (revisao explicita da paridade).
--
-- Convencao de chaves: chave pontuada armazena recurso curto
-- relativo ao modulo (saas.tenants.gerenciar => recurso='tenants').
-- As 3 chaves superadmin armazenam recurso com o prefixo do modulo
-- (saas.superadmin.dashboard / saas.superadmin.autorizacao),
-- conforme validacao do call-site do dashboard SuperAdmin.
-- Idempotente; sem dados pessoais; sem senha/chave literal.
-- ============================================================

alter table sigov.permissao add column if not exists delegavel boolean not null default false;
comment on column sigov.permissao.delegavel is
    'TRUE quando a permissao pode ser administrada/delegada dentro do envelope do cliente; FALSE para modulos sistemicos (saas, contexto). Fonte de autoridade: banco.';

-- ------------------------------------------------------------
-- 2. Chaves novas: plataforma MNSOFT (modulo saas) + delegaveis (modulo cliente)
-- ------------------------------------------------------------
insert into sigov.permissao(modulo, chave, descricao, recurso, acao, ativo, is_deleted)
values
    ('saas', 'saas.plataforma.administrar', 'Administrar a plataforma MNSOFT no SIGOV PLUS (autoridade global persistida)', 'plataforma', 'administrar', true, false),
    ('saas', 'saas.cliente.visualizar', 'Visualizar o cadastro comercial de clientes/tenants', 'cliente', 'visualizar', true, false),
    ('saas', 'saas.cliente.gerenciar', 'Gerenciar o cadastro comercial de clientes/tenants', 'cliente', 'gerenciar', true, false),
    ('saas', 'saas.assinatura.visualizar', 'Visualizar planos e assinaturas contratadas', 'assinatura', 'visualizar', true, false),
    ('saas', 'saas.assinatura.gerenciar', 'Gerenciar planos, assinaturas e modulos contratados', 'assinatura', 'gerenciar', true, false),
    ('saas', 'saas.uso.visualizar', 'Visualizar consumo e limites contraidos por cliente e modulo', 'uso', 'visualizar', true, false),
    ('saas', 'saas.bloqueio.gerenciar', 'Aplicar e liberar bloqueios comerciais sem apagar dados', 'bloqueio', 'gerenciar', true, false),
    ('cliente', 'cliente.usuarios.gerenciar', 'Gerenciar usuarios do proprio cliente dentro do envelope de delegacao', 'usuarios', 'gerenciar', true, false),
    ('cliente', 'cliente.perfis.gerenciar', 'Gerenciar perfis de acesso do proprio cliente', 'perfis', 'gerenciar', true, false),
    ('cliente', 'cliente.escopos.gerenciar', 'Gerenciar escopos de acesso do proprio cliente', 'escopos', 'gerenciar', true, false),
    ('cliente', 'cliente.contratos.visualizar', 'Visualizar contratos, modulos e limites do proprio cliente', 'contratos', 'visualizar', true, false)
on conflict (modulo, chave) do nothing;

-- ------------------------------------------------------------
-- 3. Normalizacao corretiva (chaves derivadas pela migration 013)
-- ------------------------------------------------------------
update sigov.permissao
   set recurso = 'saas.superadmin.dashboard',
       acao = 'visualizar',
       updated_at = now()
 where chave = 'saas.superadmin.dashboard.visualizar';

update sigov.permissao
   set recurso = 'saas.superadmin.dashboard',
       acao = 'exportar',
       updated_at = now()
 where chave = 'saas.superadmin.dashboard.exportar';

update sigov.permissao
   set recurso = 'saas.superadmin.autorizacao',
       acao = 'administrar',
       updated_at = now()
 where chave = 'saas.superadmin.autorizacao.administrar';

-- ------------------------------------------------------------
-- 4. Bulk delegavel (deterministico; nao toca linhas ja corretas)
-- ------------------------------------------------------------
update sigov.permissao
   set delegavel = ativo and modulo not in ('saas', 'contexto'),
       updated_at = now()
 where is_deleted = false
   and delegavel <> (ativo and modulo not in ('saas', 'contexto'));

-- ------------------------------------------------------------
-- 5. Perfis sistemicos (8 codigos 68b + ADMINISTRADOR_GERAL)
-- ------------------------------------------------------------
update sigov.perfil_acesso
   set sistemico = true,
       updated_at = coalesce(updated_at, now())
 where upper(coalesce(codigo_externo, '')) in
       ('SUPERADMIN','ADMIN_TENANT','DIRETOR_GESTOR','COORDENADOR_AREA',
        'OPERACIONAL_USUARIO','FINANCEIRO','AUDITOR_LEITURA','ATENDIMENTO',
        'ADMINISTRADOR_GERAL')
   and ativo and not is_deleted
   and sistemico is distinct from true;

-- ------------------------------------------------------------
-- 6. Grants de plataforma MNSOFT (SUPERADMIN + ADMINISTRADOR_GERAL)
-- ------------------------------------------------------------
insert into sigov.perfil_permissao(perfil_acesso_id, permissao_id, efeito, justificativa)
select pa.id,
       p.id,
       'PERMITIR',
       case when upper(pa.codigo_externo) = 'ADMINISTRADOR_GERAL' then 'LEGACY_ALIAS' else 'PLATAFORMA_MNSOFT' end
from sigov.perfil_acesso pa
join sigov.permissao p
  on p.chave in
     ('saas.plataforma.administrar',
      'saas.cliente.visualizar',
      'saas.cliente.gerenciar',
      'saas.assinatura.visualizar',
      'saas.assinatura.gerenciar',
      'saas.uso.visualizar',
      'saas.bloqueio.gerenciar',
      'saas.superadmin.dashboard.visualizar',
      'saas.superadmin.dashboard.exportar',
      'saas.superadmin.autorizacao.administrar')
where pa.tenant_id is not null
  and pa.ativo and not pa.is_deleted
  and upper(pa.codigo_externo) in ('SUPERADMIN', 'ADMINISTRADOR_GERAL')
on conflict (perfil_acesso_id, permissao_id) do nothing;

-- ------------------------------------------------------------
-- 6b. Grants delegaveis do cliente (+ ADMIN_TENANT)
-- ------------------------------------------------------------
insert into sigov.perfil_permissao(perfil_acesso_id, permissao_id, efeito, justificativa)
select pa.id,
       p.id,
       'PERMITIR',
       case when upper(pa.codigo_externo) = 'ADMINISTRADOR_GERAL' then 'LEGACY_ALIAS'
            when upper(pa.codigo_externo) = 'ADMIN_TENANT' then 'DELEGACAO_CLIENTE'
            else 'PLATAFORMA_MNSOFT' end
from sigov.perfil_acesso pa
join sigov.permissao p
  on p.chave in
     ('cliente.usuarios.gerenciar',
      'cliente.perfis.gerenciar',
      'cliente.escopos.gerenciar',
      'cliente.contratos.visualizar')
where pa.tenant_id is not null
  and pa.ativo and not pa.is_deleted
  and upper(pa.codigo_externo) in ('SUPERADMIN', 'ADMINISTRADOR_GERAL', 'ADMIN_TENANT')
on conflict (perfil_acesso_id, permissao_id) do nothing;

-- ------------------------------------------------------------
-- 7. Rastreio de aliases de admin global (paridade de transicao auditada)
-- ------------------------------------------------------------
create table if not exists sigov.saas_alias_revisao (
    id bigint generated always as identity primary key,
    alias_codigo varchar(100) not null,
    usuario_id bigint not null references sigov.usuario(id),
    perfil_acesso_id bigint not null references sigov.perfil_acesso(id),
    tenant_id bigint null,
    motivo varchar(80) not null default 'LEGACY_ALIAS',
    resolvido boolean not null default false,
    criado_em_utc timestamptz not null default now(),
    resolvido_em_utc timestamptz null,
    constraint ck_saas_alias_revisao_motivo check (length(btrim(motivo)) >= 3)
);

create index if not exists ix_saas_alias_revisao_tenant
 on sigov.saas_alias_revisao(tenant_id);
create unique index if not exists uix_saas_alias_revisao_aberto
 on sigov.saas_alias_revisao(alias_codigo, usuario_id, perfil_acesso_id)
 where resolvido = false;

insert into sigov.saas_alias_revisao(alias_codigo, usuario_id, perfil_acesso_id, tenant_id, motivo)
select pa.codigo_externo,
       ug.usuario_id,
       pa.id,
       coalesce(ug.tenant_id, pa.tenant_id),
       'LEGACY_ALIAS'
from sigov.usuario_grupo ug
join sigov.grupo_perfil gp
  on gp.grupo_acesso_id = ug.grupo_acesso_id
 and gp.ativo and not gp.is_deleted
 and (gp.vigencia_fim is null or gp.vigencia_fim > now())
join sigov.perfil_acesso pa
  on pa.id = gp.perfil_acesso_id
 and pa.ativo and not pa.is_deleted
where ug.ativo and not ug.is_deleted
  and (ug.vigencia_fim is null or ug.vigencia_fim > now())
  and upper(coalesce(pa.codigo_externo, '')) in
      ('ADMINISTRADOR_GERAL', 'SIGOV_ADMIN', 'SUPER_ADMIN', 'SUPERADMIN', 'ADMIN_GERAL')
group by pa.codigo_externo, ug.usuario_id, pa.id, coalesce(ug.tenant_id, pa.tenant_id)
on conflict (alias_codigo, usuario_id, perfil_acesso_id) where resolvido = false do nothing;
