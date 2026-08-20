-- RC50.68A: evolução não destrutiva do modelo canônico de autorização.
-- Não cria tabelas paralelas: amplia permissao e os vínculos existentes.
alter table sigov.permissao
    add column if not exists recurso varchar(160),
    add column if not exists acao varchar(80);

update sigov.permissao
   set recurso = coalesce(nullif(recurso, ''), split_part(chave, '.', 1)),
       acao = coalesce(nullif(acao, ''), case when position('.' in chave) > 0
                    then substring(chave from position('.' in chave) + 1) else 'acessar' end)
 where recurso is null or acao is null;

alter table sigov.perfil_permissao
    add column if not exists tenant_id bigint,
    add column if not exists entidade_id bigint,
    add column if not exists exercicio_id bigint,
    add column if not exists unidade_id bigint,
    add column if not exists vigencia_inicio timestamptz,
    add column if not exists vigencia_fim timestamptz,
    add column if not exists alcada_valor numeric(18,2),
    add column if not exists efeito varchar(10) not null default 'PERMITIR',
    add column if not exists justificativa varchar(500),
    add column if not exists updated_at timestamptz;

alter table sigov.grupo_perfil
    add column if not exists tenant_id bigint,
    add column if not exists entidade_id bigint,
    add column if not exists exercicio_id bigint,
    add column if not exists unidade_id bigint,
    add column if not exists vigencia_inicio timestamptz,
    add column if not exists vigencia_fim timestamptz;

alter table sigov.usuario_grupo
    add column if not exists tenant_id bigint,
    add column if not exists entidade_id bigint,
    add column if not exists exercicio_id bigint,
    add column if not exists unidade_id bigint,
    add column if not exists vigencia_inicio timestamptz,
    add column if not exists vigencia_fim timestamptz;

do $$ begin
  if not exists (select 1 from pg_constraint where conname='ck_perfil_permissao_efeito') then
    alter table sigov.perfil_permissao add constraint ck_perfil_permissao_efeito
      check (efeito in ('PERMITIR','NEGAR'));
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_perfil_permissao_vigencia') then
    alter table sigov.perfil_permissao add constraint ck_perfil_permissao_vigencia
      check (vigencia_fim is null or vigencia_inicio is null or vigencia_fim > vigencia_inicio);
  end if;
  if not exists (select 1 from pg_constraint where conname='ck_perfil_permissao_alcada') then
    alter table sigov.perfil_permissao add constraint ck_perfil_permissao_alcada
      check (alcada_valor is null or alcada_valor >= 0);
  end if;
end $$;

create index if not exists ix_perfil_permissao_escopo_vigencia
 on sigov.perfil_permissao(tenant_id,entidade_id,exercicio_id,unidade_id,efeito,vigencia_fim);
create index if not exists ix_permissao_recurso_acao
 on sigov.permissao(recurso,acao) where ativo and not is_deleted;
