\set ON_ERROR_STOP 1
create or replace function pg_temp.chain(uid int8, tid bigint) returns table(efeito text, alcada numeric) language sql as $$
select pp.efeito, pp.alcada_valor
from sigov.usuario_grupo ug
join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
join sigov.grupo_perfil gp on gp.grupo_acesso_id=ga.id and gp.ativo and not gp.is_deleted
join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted
join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
where ug.usuario_id=$1 and ug.ativo and not ug.is_deleted
  and (p.modulo='compras_empresariais' or p.modulo='*')
  and (p.recurso='aprovacoes' or p.recurso='compras_empresariais.aprovacoes' or 'aprovacoes'='compras_empresariais.'||p.recurso or p.recurso='*')
  and (p.acao='aprovar' or p.acao='*')
  and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>=now())
  and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>=now())
  and (pp.vigencia_inicio is null or pp.vigencia_inicio<=now()) and (pp.vigencia_fim is null or pp.vigencia_fim>=now())
  and (ug.tenant_id is null or ug.tenant_id=$2) and (gp.tenant_id is null or gp.tenant_id=$2) and (pp.tenant_id is null or pp.tenant_id=$2);
$$;

select 'e_t1_50k:'||coalesce(string_agg(x.sub::text,',' order by x.sub),'-') from (
  select distinct md5('sigov:usuario:'||u.id::text)::uuid sub from sigov.usuario u
  where u.ativo and not u.is_deleted
    and exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='PERMITIR' and (c.alcada is null or c.alcada>=50000))
    and not exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='NEGAR')
) x;

select 'e_t1_250k:'||coalesce(string_agg(x.sub::text,',' order by x.sub),'-') from (
  select distinct md5('sigov:usuario:'||u.id::text)::uuid sub from sigov.usuario u
  where u.ativo and not u.is_deleted
    and exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='PERMITIR' and (c.alcada is null or c.alcada>=250000))
    and not exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='NEGAR')
) x;

select 'e_t1_1bil:'||coalesce(string_agg(x.sub::text,',' order by x.sub),'-') from (
  select distinct md5('sigov:usuario:'||u.id::text)::uuid sub from sigov.usuario u
  where u.ativo and not u.is_deleted
    and exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='PERMITIR' and (c.alcada is null or c.alcada>=1000000000))
    and not exists(select 1 from pg_temp.chain(u.id,1) c where c.efeito='NEGAR')
) x;

select 'e_t5_50k:'||coalesce(string_agg(x.sub::text,',' order by x.sub),'-') from (
  select distinct md5('sigov:usuario:'||u.id::text)::uuid sub from sigov.usuario u
  where u.ativo and not u.is_deleted
    and exists(select 1 from pg_temp.chain(u.id,5) c where c.efeito='PERMITIR' and (c.alcada is null or c.alcada>=50000))
    and not exists(select 1 from pg_temp.chain(u.id,5) c where c.efeito='NEGAR')
) x;

select 'sub101:'||md5('sigov:usuario:101')::uuid::text, 'sub102:'||md5('sigov:usuario:102')::uuid::text, 'subadmin:'||md5('sigov:usuario:1')::uuid::text;

drop function pg_temp.chain(int8,bigint);

select 'cnt:'||t||'|'||cnt from (values
  ('map', (select count(*) from sigov.enterprise_tenant_mapping)),
  ('ent_nova', (select count(*) from sigov.entidade where id in (9101,9102,9103))),
  ('uni_nova', (select count(*) from sigov.unidade_organizacional where id in (9201,9202,9203))),
  ('usr_demo', (select count(*) from sigov.usuario where id in (101,102))),
  ('perf_demo', (select count(*) from sigov.perfil_acesso where id in (9001,9002))),
  ('grpo_demo', (select count(*) from sigov.grupo_acesso where id in (9301,9302))),
  ('gp', (select count(*) from sigov.grupo_perfil where grupo_acesso_id in (9301,9302))),
  ('ug', (select count(*) from sigov.usuario_grupo where usuario_id in (101,102))),
  ('pp', (select count(*) from sigov.perfil_permissao where perfil_acesso_id in (9001,9002))),
  ('os_demo', (select count(*) from sigov.os_tecnico where created_by='seed')),
  ('rq_demo', (select count(*) from sigov.compras_empresarial_requisicao where numero like 'RC-DEMO-%')),
  ('it_demo', (select count(*) from sigov.compras_empresarial_requisicao_item i join sigov.compras_empresarial_requisicao r on r.id=i.requisicao_id where r.numero like 'RC-DEMO-%'))
) as v(t,cnt) order by t;
