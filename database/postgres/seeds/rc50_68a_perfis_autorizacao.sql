-- Seed fictícia e idempotente: catálogo persistente, sem usuários ou credenciais.
do $$
declare r record; p record; v_perfil_id bigint; v_permissao_id bigint;
begin
  for r in select t.id tenant_id, x.codigo, x.nome, x.descricao
    from sigov.tenant t cross join (values
      ('SUPERADMIN','Superadministrador','Administração global da plataforma'),
      ('ADMIN_TENANT','Administrador do tenant','Administração do tenant'),
      ('DIRETOR_GESTOR','Diretor gestor','Gestão e aprovação institucional'),
      ('COORDENADOR_AREA','Coordenador de área','Coordenação de unidade ou área'),
      ('OPERACIONAL_USUARIO','Usuário operacional','Execução operacional'),
      ('FINANCEIRO','Financeiro','Operação e aprovação financeira'),
      ('AUDITOR_LEITURA','Auditor de leitura','Auditoria somente leitura'),
      ('ATENDIMENTO','Atendimento','Atendimento ao cidadão')
    ) x(codigo,nome,descricao) where not coalesce(t.is_deleted,false)
  loop
    insert into sigov.perfil_acesso(tenant_id,nome,descricao,codigo_externo,ativo,is_deleted)
    select r.tenant_id,r.nome,r.descricao,r.codigo,true,false
    where not exists(select 1 from sigov.perfil_acesso where tenant_id=r.tenant_id and codigo_externo=r.codigo and not is_deleted);
    update sigov.perfil_acesso set nome=r.nome,descricao=r.descricao,ativo=true,is_deleted=false,updated_at=now()
     where tenant_id=r.tenant_id and codigo_externo=r.codigo;
    select id into v_perfil_id from sigov.perfil_acesso where tenant_id=r.tenant_id and codigo_externo=r.codigo and not is_deleted order by id limit 1;

    for p in select * from (values
      ('AUTORIZACAO','autorizacao.catalogo','ler','Consulta ao catálogo persistente'),
      ('AUTORIZACAO','autorizacao.atribuicao','gerenciar','Gestão persistente de atribuições')
    ) q(modulo,chave,acao,descricao)
    loop
      insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
      values(p.modulo,p.chave,split_part(p.chave,'.',1),p.acao,p.descricao,true,false)
      on conflict(modulo,chave) do update set recurso=excluded.recurso,acao=excluded.acao,descricao=excluded.descricao,ativo=true,is_deleted=false;
      select id into v_permissao_id from sigov.permissao where modulo=p.modulo and chave=p.chave;
      if r.codigo in ('SUPERADMIN','ADMIN_TENANT') or (r.codigo='AUDITOR_LEITURA' and p.acao='ler') then
        insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,tenant_id,efeito)
        values(v_perfil_id,v_permissao_id,r.tenant_id,'PERMITIR')
        on conflict(perfil_acesso_id,permissao_id) do update set tenant_id=excluded.tenant_id,efeito='PERMITIR',updated_at=now();
      end if;
    end loop;
  end loop;
end $$;
