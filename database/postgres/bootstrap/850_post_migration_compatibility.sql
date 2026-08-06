-- SIGOV+ RC38E
-- Compatibilidade mínima exigida pelo bootstrap operacional após todas as migrations.

create extension if not exists pgcrypto;

alter table sigov.usuario add column if not exists tenant_id bigint null;
alter table sigov.usuario add column if not exists nome varchar(200) null;
alter table sigov.usuario add column if not exists tipo_usuario varchar(80) null;
alter table sigov.usuario add column if not exists senha_deve_ser_alterada boolean not null default false;
alter table sigov.usuario add column if not exists deve_alterar_senha boolean not null default false;
alter table sigov.usuario add column if not exists bloqueado boolean not null default false;
alter table sigov.usuario add column if not exists tentativas_invalidas integer not null default 0;
alter table sigov.usuario add column if not exists bloqueado_ate timestamptz null;

alter table sigov.entidade add column if not exists tenant_id bigint null;
alter table sigov.exercicio add column if not exists tenant_id bigint null;
alter table sigov.pessoa add column if not exists tenant_id bigint null;
alter table sigov.grupo_acesso add column if not exists tenant_id bigint null;
alter table sigov.perfil_acesso add column if not exists tenant_id bigint null;
alter table sigov.usuario_grupo add column if not exists tenant_id bigint null;
alter table sigov.usuario_grupo add column if not exists is_deleted boolean not null default false;
alter table sigov.grupo_perfil add column if not exists tenant_id bigint null;
alter table sigov.grupo_perfil add column if not exists is_deleted boolean not null default false;
alter table sigov.perfil_permissao add column if not exists tenant_id bigint null;
alter table sigov.usuario_entidade add column if not exists tenant_id bigint null;
alter table sigov.usuario_exercicio add column if not exists tenant_id bigint null;
alter table sigov.politica_senha add column if not exists tenant_id bigint null;

alter table sigov.tenant_feature_flag add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_feature_flag add column if not exists feature_codigo varchar(120) null;
alter table sigov.tenant_feature_flag add column if not exists habilitada boolean not null default false;
alter table sigov.tenant_feature_flag add column if not exists parametros_json jsonb not null default '{}'::jsonb;

alter table sigov.tenant_parametro_valor add column if not exists usuario_id bigint null;
alter table sigov.tenant_parametro_valor add column if not exists modulo_codigo varchar(80) null;
alter table sigov.tenant_parametro_valor add column if not exists escopo varchar(40) not null default 'TENANT';

-- Migrations históricas inserem apenas as chaves dos relacionamentos. Depois que o
-- schema é tenantizado, tenant_id passa a ser obrigatório. O trigger deriva o tenant
-- da entidade principal do vínculo, permitindo reexecutar as migrations sem alterar
-- seus checksums e sem criar vínculos fora do tenant correto.
create or replace function sigov.fn_preencher_tenant_vinculo()
returns trigger
language plpgsql
as $$
begin
    if new.tenant_id is not null then
        return new;
    end if;

    case tg_table_name
        when 'usuario_entidade' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select e.tenant_id into new.tenant_id
                  from sigov.entidade e
                 where e.id = new.entidade_id;
            end if;

        when 'usuario_exercicio' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select x.tenant_id into new.tenant_id
                  from sigov.exercicio x
                 where x.id = new.exercicio_id;
            end if;

        when 'usuario_grupo' then
            select u.tenant_id into new.tenant_id
              from sigov.usuario u
             where u.id = new.usuario_id;
            if new.tenant_id is null then
                select g.tenant_id into new.tenant_id
                  from sigov.grupo_acesso g
                 where g.id = new.grupo_acesso_id;
            end if;

        when 'grupo_perfil' then
            select g.tenant_id into new.tenant_id
              from sigov.grupo_acesso g
             where g.id = new.grupo_acesso_id;
            if new.tenant_id is null then
                select p.tenant_id into new.tenant_id
                  from sigov.perfil_acesso p
                 where p.id = new.perfil_acesso_id;
            end if;

        when 'perfil_permissao' then
            select p.tenant_id into new.tenant_id
              from sigov.perfil_acesso p
             where p.id = new.perfil_acesso_id;
    end case;

    if new.tenant_id is null then
        raise exception 'Não foi possível determinar tenant_id para %.', tg_table_name
            using errcode = '23502';
    end if;

    return new;
end $$;

drop trigger if exists trg_usuario_entidade_tenant on sigov.usuario_entidade;
create trigger trg_usuario_entidade_tenant
before insert or update on sigov.usuario_entidade
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_usuario_exercicio_tenant on sigov.usuario_exercicio;
create trigger trg_usuario_exercicio_tenant
before insert or update on sigov.usuario_exercicio
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_usuario_grupo_tenant on sigov.usuario_grupo;
create trigger trg_usuario_grupo_tenant
before insert or update on sigov.usuario_grupo
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_grupo_perfil_tenant on sigov.grupo_perfil;
create trigger trg_grupo_perfil_tenant
before insert or update on sigov.grupo_perfil
for each row execute function sigov.fn_preencher_tenant_vinculo();

drop trigger if exists trg_perfil_permissao_tenant on sigov.perfil_permissao;
create trigger trg_perfil_permissao_tenant
before insert or update on sigov.perfil_permissao
for each row execute function sigov.fn_preencher_tenant_vinculo();

-- Bancos que já foram executados parcialmente podem possuir perfis e grupos
-- duplicados por tenant. Antes de criar os índices únicos usados pelo bootstrap,
-- preservamos todos os registros e seus vínculos, mantendo como canônico o item
-- com maior uso em relacionamentos e renomeando apenas a chave/nome dos legados.
do $$
begin
    if to_regclass('sigov.perfil_acesso') is not null then
        with perfil_uso as (
            select p.id,
                   count(distinct gp.grupo_acesso_id) + count(distinct pp.permissao_id) as total_vinculos
              from sigov.perfil_acesso p
              left join sigov.grupo_perfil gp on gp.perfil_acesso_id = p.id
              left join sigov.perfil_permissao pp on pp.perfil_acesso_id = p.id
             group by p.id
        ), perfil_rank as (
            select p.id,
                   p.codigo_externo,
                   row_number() over (
                       partition by p.tenant_id, p.codigo_externo
                       order by coalesce(u.total_vinculos, 0) desc,
                                case when p.ativo then 0 else 1 end,
                                p.created_at nulls last,
                                p.id
                   ) as rn
              from sigov.perfil_acesso p
              left join perfil_uso u on u.id = p.id
             where p.tenant_id is not null
               and nullif(p.codigo_externo, '') is not null
               and p.is_deleted = false
        )
        update sigov.perfil_acesso p
           set codigo_externo = concat(
                   left(r.codigo_externo, greatest(1, 100 - length('_LEGACY_' || p.id::text))),
                   '_LEGACY_',
                   p.id::text
               ),
               observacao = concat_ws(E'\n',
                   nullif(p.observacao, ''),
                   'Código externo legado ajustado para remover duplicidade antes do índice ux_bootstrap_perfil_codigo_tenant. Código anterior: ' || r.codigo_externo
               ),
               updated_at = now()
          from perfil_rank r
         where p.id = r.id
           and r.rn > 1;
    end if;

    if to_regclass('sigov.grupo_acesso') is not null then
        with grupo_uso as (
            select g.id,
                   count(distinct ug.usuario_id) + count(distinct gp.perfil_acesso_id) as total_vinculos
              from sigov.grupo_acesso g
              left join sigov.usuario_grupo ug on ug.grupo_acesso_id = g.id
              left join sigov.grupo_perfil gp on gp.grupo_acesso_id = g.id
             group by g.id
        ), grupo_rank as (
            select g.id,
                   g.nome,
                   row_number() over (
                       partition by g.tenant_id, g.nome
                       order by coalesce(u.total_vinculos, 0) desc,
                                case when g.ativo then 0 else 1 end,
                                g.created_at nulls last,
                                g.id
                   ) as rn
              from sigov.grupo_acesso g
              left join grupo_uso u on u.id = g.id
             where g.tenant_id is not null
               and nullif(g.nome, '') is not null
               and g.is_deleted = false
        )
        update sigov.grupo_acesso g
           set nome = concat(
                   left(r.nome, greatest(1, 150 - length(' (legado ' || g.id::text || ')'))),
                   ' (legado ',
                   g.id::text,
                   ')'
               ),
               observacao = concat_ws(E'\n',
                   nullif(g.observacao, ''),
                   'Nome legado ajustado para remover duplicidade antes do índice ux_bootstrap_grupo_nome_tenant. Nome anterior: ' || r.nome
               ),
               updated_at = now()
          from grupo_rank r
         where g.id = r.id
           and r.rn > 1;
    end if;
end $$;

create unique index if not exists ux_bootstrap_usuario_login_tenant
    on sigov.usuario (tenant_id, lower(login)) where is_deleted = false;
create unique index if not exists ux_bootstrap_usuario_email_tenant
    on sigov.usuario (tenant_id, lower(email)) where is_deleted = false;
create unique index if not exists ux_bootstrap_perfil_codigo_tenant
    on sigov.perfil_acesso (tenant_id, codigo_externo) where codigo_externo is not null and is_deleted = false;
create unique index if not exists ux_bootstrap_grupo_nome_tenant
    on sigov.grupo_acesso (tenant_id, nome) where is_deleted = false;