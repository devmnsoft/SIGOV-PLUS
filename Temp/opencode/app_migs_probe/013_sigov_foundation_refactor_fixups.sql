create table if not exists sigov.controle_sequencial (
    id bigint generated always as identity primary key,
    entidade_id bigint null references sigov.entidade(id),
    exercicio_id bigint null references sigov.exercicio(id),
    chave varchar(100) not null,
    ano integer not null,
    ultimo_numero bigint not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (entidade_id, exercicio_id, chave, ano)
);

alter table sigov.usuario add column if not exists senha_deve_ser_alterada boolean not null default false;
alter table sigov.usuario add column if not exists tentativas_invalidas integer not null default 0;
alter table sigov.usuario add column if not exists bloqueado_ate timestamptz null;

alter table sigov.usuario_grupo add column if not exists is_deleted boolean not null default false;

alter table sigov.permissao add column if not exists recurso varchar(100) not null default 'geral';
alter table sigov.permissao add column if not exists acao varchar(50) not null default 'visualizar';
alter table sigov.permissao add column if not exists critica boolean not null default false;

update sigov.permissao
set recurso = split_part(chave, '.', 1),
    acao = case when strpos(chave, '.') > 0 then split_part(chave, '.', 2) else 'administrar' end,
    critica = chave like '%admin%'
where (recurso = 'geral' and acao = 'visualizar') or chave like '%admin%';

do $$
begin
    if not exists (
        select 1 from pg_constraint
        where conname = 'uk_sigov_permissao_modulo_recurso_acao'
          and conrelid = 'sigov.permissao'::regclass
    ) then
        alter table sigov.permissao add constraint uk_sigov_permissao_modulo_recurso_acao unique (modulo, recurso, acao);
    end if;
end $$;

create table if not exists sigov.usuario_entidade (
    usuario_id bigint not null references sigov.usuario(id),
    entidade_id bigint not null references sigov.entidade(id),
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    created_by bigint null,
    correlation_id uuid null,
    primary key (usuario_id, entidade_id)
);

create table if not exists sigov.usuario_exercicio (
    usuario_id bigint not null references sigov.usuario(id),
    exercicio_id bigint not null references sigov.exercicio(id),
    ativo boolean not null default true,
    created_at timestamptz not null default now(),
    created_by bigint null,
    correlation_id uuid null,
    primary key (usuario_id, exercicio_id)
);

create table if not exists sigov.grupo_perfil (
    grupo_acesso_id bigint not null references sigov.grupo_acesso(id),
    perfil_acesso_id bigint not null references sigov.perfil_acesso(id),
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    correlation_id uuid null,
    primary key (grupo_acesso_id, perfil_acesso_id)
);

insert into sigov.usuario_entidade (usuario_id, entidade_id)
select u.id, u.entidade_id
from sigov.usuario u
where u.entidade_id is not null
on conflict do nothing;

insert into sigov.usuario_exercicio (usuario_id, exercicio_id)
select u.id, u.exercicio_id
from sigov.usuario u
where u.exercicio_id is not null
on conflict do nothing;

insert into sigov.usuario_grupo (usuario_id, grupo_acesso_id)
select u.id, g.id
from sigov.usuario u
join sigov.grupo_acesso g on g.entidade_id = u.entidade_id and g.nome = 'Administradores' and g.is_deleted = false
where u.login = 'admin' and u.is_deleted = false
on conflict do nothing;

insert into sigov.grupo_perfil (grupo_acesso_id, perfil_acesso_id)
select g.id, p.id
from sigov.grupo_acesso g
join sigov.perfil_acesso p on p.entidade_id = g.entidade_id and p.nome = 'Administrador' and p.is_deleted = false
where g.nome = 'Administradores' and g.is_deleted = false
on conflict do nothing;

create index if not exists idx_usuario_login on sigov.usuario (login) where is_deleted = false;
create index if not exists idx_usuario_email on sigov.usuario (email) where is_deleted = false;
create index if not exists idx_permissao_modulo_recurso_acao on sigov.permissao (modulo, recurso, acao) where is_deleted = false;
create index if not exists idx_controle_sequencial_chave on sigov.controle_sequencial (chave, ano);
