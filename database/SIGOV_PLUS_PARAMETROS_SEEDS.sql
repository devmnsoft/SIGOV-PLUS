-- SIGOV PLUS RC50.95 - dados exclusivamente locais/de desenvolvimento.
-- A senha nunca é armazenada em texto puro: senha_hash usa SIGOV_PBKDF2_V1.
begin;

insert into sigov.plano_saas(codigo,nome,descricao,ativo)
values ('IMPLANTACAO','Implantação','Configuração inicial assistida',true),
       ('ESSENCIAL','Essencial','Operação pública essencial',true),
       ('PROFISSIONAL','Profissional','Operação integrada multi-esfera',true),
       ('ENTERPRISE','Enterprise','Operação ampla e governança avançada',true)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,ativo=true,is_deleted=false;

insert into sigov.tenant(nome,nome_fantasia,slug,status,ambiente,metadados,ativo)
values ('MNSOFT Administração Global','MNSOFT','mnsoft-global','ATIVO','DEVELOPMENT','{"uso":"local-dev","esfera":"outro"}',true),
       ('Prefeitura de Santa Clara','Santa Clara','santa-clara','ATIVO','DEVELOPMENT','{"uso":"exemplo-ficticio","esfera":"municipal","tipo_entidade":"prefeitura"}',true),
       ('SEFAZ Estadual','SEFAZ Exemplo','sefaz-estadual','ATIVO','DEVELOPMENT','{"uso":"exemplo-ficticio","esfera":"estadual","tipo_entidade":"sefaz"}',true),
       ('Órgão Federal Exemplo','Órgão Federal Exemplo','orgao-federal-exemplo','ATIVO','DEVELOPMENT','{"uso":"exemplo-ficticio","esfera":"federal","tipo_entidade":"autarquia"}',true)
on conflict(slug) do update set nome=excluded.nome,nome_fantasia=excluded.nome_fantasia,status='ATIVO',ambiente='DEVELOPMENT',metadados=excluded.metadados,ativo=true,is_deleted=false;

insert into sigov.entidade(tenant_id,nome,cnpj,esfera_governo,tipo_entidade,hierarquia_administrativa,abrangencia_territorial,uf,municipio,regiao_jurisdicao,ativo)
select t.id,v.nome,v.cnpj,v.esfera,v.tipo,v.hierarquia,v.abrangencia,v.uf,v.municipio,v.jurisdicao,true
from (values
 ('mnsoft-global','MNSOFT Administração Global','00000000000100','federal','outro','Administração global local','Nacional',null,null,'Brasil'),
 ('santa-clara','Prefeitura de Santa Clara','00000000000290','municipal','prefeitura','Administração direta','Municipal','SC','Santa Clara','Município fictício'),
 ('sefaz-estadual','SEFAZ Estadual','00000000000370','estadual','sefaz','Secretaria estadual','Estadual','SP',null,'Estado fictício'),
 ('orgao-federal-exemplo','Órgão Federal Exemplo','00000000000450','federal','autarquia','Autarquia federal','Nacional','DF',null,'Brasil')
) v(slug,nome,cnpj,esfera,tipo,hierarquia,abrangencia,uf,municipio,jurisdicao)
join sigov.tenant t on t.slug=v.slug
where not exists(select 1 from sigov.entidade e where e.tenant_id=t.id and e.cnpj=v.cnpj);

insert into sigov.perfil_acesso(tenant_id,nome,descricao,ativo)
select t.id,p.nome,p.descricao,true from sigov.tenant t cross join (values
 ('Super Administrador MNSOFT','Administração global local/dev'),('Administrador do Cliente','Administração do tenant'),
 ('Gestor do Módulo','Gestão funcional'),('Operador','Operação autorizada'),('Consulta/Auditoria','Consulta e auditoria'),
 ('Portal do Cidadão','Autosserviço do cidadão'),('Portal do Servidor','Autosserviço do servidor')
) p(nome,descricao) where t.slug='mnsoft-global'
and not exists(select 1 from sigov.perfil_acesso x where x.tenant_id=t.id and x.nome=p.nome and not x.is_deleted);

insert into sigov.usuario(tenant_id,entidade_id,login,email,nome,senha_hash,tipo_usuario,ativo,bloqueado,deve_alterar_senha,senha_deve_ser_alterada,observacao)
select t.id,e.id,'superadmin@mnsoft.local','superadmin@mnsoft.local','Super Administrador MNSOFT',
 'SIGOV_PBKDF2_V1$100000$4MWYL8/raSySpaBAVXpMug==$Z67qILa4EHjrZPrGnuWoBqorsMDZN663xw+nZZFqkHg=',
 'ADMINISTRADOR_GERAL',true,false,true,true,'USO EXCLUSIVO LOCAL/DEV; troca obrigatória no primeiro acesso'
from sigov.tenant t join sigov.entidade e on e.tenant_id=t.id
where t.slug='mnsoft-global'
and not exists(select 1 from sigov.usuario u where u.tenant_id=t.id and lower(u.email)='superadmin@mnsoft.local' and not u.is_deleted);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id)
select pa.id,p.id from sigov.perfil_acesso pa join sigov.tenant t on t.id=pa.tenant_id cross join sigov.permissao p
where t.slug='mnsoft-global' and pa.nome='Super Administrador MNSOFT' and p.ativo and not p.is_deleted
on conflict do nothing;

commit;
