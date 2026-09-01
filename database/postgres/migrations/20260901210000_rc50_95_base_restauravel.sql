-- RC50.95 - contexto institucional multi-esfera para a base restaurável.
alter table sigov.entidade add column if not exists esfera_governo varchar(12);
alter table sigov.entidade add column if not exists tipo_entidade varchar(80);
alter table sigov.entidade add column if not exists orgao_superior_id bigint references sigov.entidade(id);
alter table sigov.entidade add column if not exists unidade_gestora_id bigint;
alter table sigov.entidade add column if not exists unidade_executora_id bigint;
alter table sigov.entidade add column if not exists hierarquia_administrativa text;
alter table sigov.entidade add column if not exists abrangencia_territorial text;
alter table sigov.entidade add column if not exists uf char(2);
alter table sigov.entidade add column if not exists municipio text;
alter table sigov.entidade add column if not exists regiao_jurisdicao text;

do $$ begin
  if not exists (select 1 from pg_constraint where conname='ck_entidade_esfera_governo') then
    alter table sigov.entidade add constraint ck_entidade_esfera_governo
      check (esfera_governo is null or esfera_governo in ('municipal','estadual','federal'));
  end if;
end $$;

create index if not exists ix_entidade_contexto_institucional
  on sigov.entidade(tenant_id, esfera_governo, tipo_entidade, uf);
