-- RC50.67 - massa operacional idempotente para homologacao integrada.
-- Uso exclusivo em DEVELOPMENT/HOMOLOGATION. Nunca contem senhas em texto claro.
\set ON_ERROR_STOP on
set search_path to sigov, public;

-- A guarda canonica cria tenant, entidade, exercicio, admin/superadmin, grupos,
-- escopos e habilita todos os modulos. Os hashes PBKDF2 abaixo sao os mesmos
-- gerados pelo provedor de identidade para as credenciais locais documentadas.
\ir development/999_super_admin_access_guard.sql

do $seed$
declare
    v_tenant_id bigint;
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_login text;
    v_nome text;
    v_perfil text;
    v_user_id bigint;
    v_pessoa_id bigint;
    v_perfil_id bigint;
    v_grupo_id bigint;
    v_seq integer := 10;
    v_hash constant text := 'SIGOV_PBKDF2_V1$210000$U0lHT1ZfREVWX1NBTFQhIQ==$kKnj2QPLDyk92OudwUguJk6BJV8qHTDJTvWv+v9JLxQ=';
begin
    if upper(coalesce(current_setting('sigov.environment', true), 'DEVELOPMENT')) not in ('DEVELOPMENT','HOMOLOGATION') then
        raise exception 'seed_homologacao_funcional.sql recusado fora de Development/Homologation';
    end if;

    select id into strict v_tenant_id from sigov.tenant where slug = 'sigov-local';
    select id into strict v_entidade_id from sigov.entidade
     where tenant_id = v_tenant_id and cnpj = '00000000000000' and not is_deleted;
    select id into strict v_exercicio_id from sigov.exercicio
     where entidade_id = v_entidade_id and ano = extract(year from current_date)::integer;

    for v_login, v_nome, v_perfil in
        select * from (values
          ('gestor.fazenda','Gestor Fazenda','GESTOR_FAZENDA'),
          ('funcionario.financeiro','Funcionario Financeiro','FUNCIONARIO_FINANCEIRO'),
          ('gestor.educacao','Gestor Educacao','GESTOR_EDUCACAO'),
          ('professor','Professor Homologacao','PROFESSOR'),
          ('gestor.saude','Gestor Saude','GESTOR_SAUDE'),
          ('acs','ACS Homologacao','ACS'),
          ('atendimento','Atendimento Homologacao','ATENDIMENTO'),
          ('auditor','Auditor Homologacao','AUDITOR'),
          ('gestor.agro','Gestor Agro','GESTOR_AGRO'),
          ('tecnico.rural','Tecnico Rural','TECNICO_RURAL'),
          ('operador.patrulha','Operador Patrulha','OPERADOR_PATRULHA')
        ) as usuarios(login,nome,perfil)
    loop
        insert into sigov.perfil_acesso
            (tenant_id,entidade_id,exercicio_id,nome,codigo_externo,ativo,is_deleted)
        select v_tenant_id,v_entidade_id,v_exercicio_id,v_nome,v_perfil,true,false
         where not exists (select 1 from sigov.perfil_acesso
                            where tenant_id=v_tenant_id and codigo_externo=v_perfil);
        update sigov.perfil_acesso set nome=v_nome,ativo=true,is_deleted=false,updated_at=now()
         where tenant_id=v_tenant_id and codigo_externo=v_perfil;
        select id into strict v_perfil_id from sigov.perfil_acesso
         where tenant_id=v_tenant_id and codigo_externo=v_perfil order by id desc limit 1;

        insert into sigov.grupo_acesso
            (tenant_id,entidade_id,exercicio_id,nome,descricao,ativo,is_deleted)
        select v_tenant_id,v_entidade_id,v_exercicio_id,v_perfil,'Perfil funcional RC50.67',true,false
         where not exists (select 1 from sigov.grupo_acesso
                            where tenant_id=v_tenant_id and nome=v_perfil);
        select id into strict v_grupo_id from sigov.grupo_acesso
         where tenant_id=v_tenant_id and nome=v_perfil order by id desc limit 1;
        insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,is_deleted)
        values(v_grupo_id,v_perfil_id,false)
        on conflict(grupo_acesso_id,perfil_acesso_id) do update set is_deleted=false;

        select id into v_user_id from sigov.usuario
         where tenant_id=v_tenant_id and lower(login)=v_login and not is_deleted limit 1;
        select id into v_pessoa_id from sigov.pessoa
         where tenant_id=v_tenant_id and documento=lpad(v_seq::text,14,'0') and not is_deleted limit 1;
        if v_pessoa_id is null then
            insert into sigov.pessoa
                (tenant_id,entidade_id,exercicio_id,tipo_pessoa,nome,documento,ativo,is_deleted)
            values(v_tenant_id,v_entidade_id,v_exercicio_id,'F',v_nome,lpad(v_seq::text,14,'0'),true,false)
            returning id into v_pessoa_id;
        end if;
        if v_user_id is null then
            insert into sigov.usuario
                (tenant_id,entidade_id,exercicio_id,pessoa_id,nome,login,email,senha_hash,tipo_usuario,
                 senha_deve_ser_alterada,deve_alterar_senha,bloqueado,tentativas_invalidas,ativo,is_deleted)
            values(v_tenant_id,v_entidade_id,v_exercicio_id,v_pessoa_id,v_nome,v_login,
                   v_login||'@invalid.local',v_hash,v_perfil,false,false,false,0,true,false)
            returning id into v_user_id;
        else
            update sigov.usuario set senha_hash=v_hash,ativo=true,bloqueado=false,is_deleted=false,
                tentativas_invalidas=0,updated_at=now() where id=v_user_id;
        end if;
        insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,is_deleted)
        values(v_user_id,v_grupo_id,false)
        on conflict(usuario_id,grupo_acesso_id) do update set is_deleted=false;
        insert into sigov.usuario_entidade(usuario_id,entidade_id,ativo)
        values(v_user_id,v_entidade_id,true) on conflict(usuario_id,entidade_id) do update set ativo=true;
        insert into sigov.usuario_exercicio(usuario_id,exercicio_id,ativo)
        values(v_user_id,v_exercicio_id,true) on conflict(usuario_id,exercicio_id) do update set ativo=true;
        v_seq := v_seq + 1;
    end loop;

    -- Grants seguem menor privilegio por prefixo; o backend continua sendo a autoridade.
    insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id)
    select pa.id,p.id from sigov.perfil_acesso pa cross join sigov.permissao p
     where pa.tenant_id=v_tenant_id and not pa.is_deleted and p.ativo and not p.is_deleted
       and ((pa.codigo_externo='GESTOR_FAZENDA' and p.modulo in ('tributario','financeiro'))
         or (pa.codigo_externo='FUNCIONARIO_FINANCEIRO' and p.modulo='financeiro')
         or (pa.codigo_externo in ('GESTOR_EDUCACAO','PROFESSOR') and p.modulo='educacao')
         or (pa.codigo_externo in ('GESTOR_SAUDE','ACS') and p.modulo='saude')
         or (pa.codigo_externo='ATENDIMENTO' and p.modulo in ('protocolo','ouvidoria','esic'))
         or (pa.codigo_externo='AUDITOR' and p.modulo in ('auditoria','lgpd'))
         or (pa.codigo_externo in ('GESTOR_AGRO','TECNICO_RURAL','OPERADOR_PATRULHA') and p.modulo in ('agro','geo')))
    on conflict do nothing;
end
$seed$;

-- Massa documental/transversal segura, ja usada pelo runtime e sem PII real.
\ir pos_rc_homologacao_demo.sql

-- Marcadores operacionais idempotentes permitem comprovar cobertura de todos os
-- dominios sem inventar colunas nas tabelas setoriais. Workers os processam pelo outbox.
with tenant_hml as (select id from sigov.tenant where slug='sigov-local'),
dominios(modulo) as (values ('tributario'),('financeiro'),('saneamento'),('educacao'),('saude'),
 ('processos'),('ged'),('rh'),('folha'),('compras'),('licitacoes'),('contratos'),('almoxarifado'),
 ('patrimonio'),('frotas'),('obras'),('agro'),('geo'),('pendencias'),('alertas'),
 ('qualidade-dados'),('integracoes-internas'))
insert into sigov.outbox_evento
    (tenant_id,event_id,event_type,aggregate_type,aggregate_id,payload,status,idempotency_key)
select t.id,gen_random_uuid(),'homologacao.massa-disponivel',d.modulo,'RC50.67-'||d.modulo,
       jsonb_build_object('modulo',d.modulo,'ambiente','HOMOLOGACAO','pii',false),
       'PENDING','rc50.67:massa:'||d.modulo
  from tenant_hml t cross join dominios d
on conflict(idempotency_key) do update set payload=excluded.payload;
