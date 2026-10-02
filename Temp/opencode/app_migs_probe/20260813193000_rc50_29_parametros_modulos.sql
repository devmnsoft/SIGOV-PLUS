-- RC50.29 - parâmetros funcionais por tenant. Database=postgres, schema=sigov.
create table if not exists sigov.parametro_modulo (
 id bigserial primary key, modulo varchar(40) not null, codigo varchar(100) not null, nome varchar(180) not null,
 descricao text null, tipo varchar(20) not null, valor_padrao jsonb not null, sensivel boolean not null default false,
 ordem integer not null default 0, ativo boolean not null default true, created_at timestamptz not null default now(),
 updated_at timestamptz null, is_deleted boolean not null default false,
 constraint ck_parametro_modulo_tipo check (tipo in ('BOOLEAN','INTEGER','DECIMAL','TEXT','JSON')));
create unique index if not exists ux_parametro_modulo_codigo on sigov.parametro_modulo(modulo,codigo) where is_deleted=false;

create table if not exists sigov.parametro_modulo_valor (
 id bigserial primary key, tenant_id bigint not null, parametro_id bigint not null references sigov.parametro_modulo(id),
 valor jsonb not null, created_at timestamptz not null default now(), updated_at timestamptz null,
 created_by bigint null, updated_by bigint null, correlation_id varchar(100) not null, is_deleted boolean not null default false);
create unique index if not exists ux_parametro_modulo_valor_tenant on sigov.parametro_modulo_valor(tenant_id,parametro_id) where is_deleted=false;
create index if not exists ix_parametro_modulo_valor_consulta on sigov.parametro_modulo_valor(tenant_id,parametro_id,updated_at desc) where is_deleted=false;

create table if not exists sigov.parametro_modulo_historico (
 id bigserial primary key, tenant_id bigint not null, parametro_id bigint not null references sigov.parametro_modulo(id),
 valor_anterior jsonb null, valor_novo jsonb not null, usuario_id bigint null, correlation_id varchar(100) not null,
 auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now());
create index if not exists ix_parametro_modulo_historico on sigov.parametro_modulo_historico(tenant_id,parametro_id,created_at desc,id desc);

insert into sigov.parametro_modulo(modulo,codigo,nome,tipo,valor_padrao,ordem)
values
('EDUCACAO','FREQUENCIA_MINIMA','Percentual mínimo de frequência','DECIMAL','75',10),
('EDUCACAO','ESCALA_NOTAS','Escala de notas','DECIMAL','10',20),('EDUCACAO','MEDIA_APROVACAO','Média mínima de aprovação','DECIMAL','6',30),
('EDUCACAO','MEDIA_RECUPERACAO','Média de recuperação','DECIMAL','5',40),('EDUCACAO','CAPACIDADE_TURMA_PADRAO','Capacidade padrão da turma','INTEGER','30',50),
('EDUCACAO','PERMITIR_EXCEDER_CAPACIDADE','Permitir matrícula acima da capacidade','BOOLEAN','false',60),('EDUCACAO','EXIGIR_RESPONSAVEL','Exigir responsável','BOOLEAN','true',70),
('EDUCACAO','EXIGIR_DOCUMENTO_ALUNO','Exigir documento do aluno','BOOLEAN','true',80),('EDUCACAO','EXIGIR_DOCUMENTO_RESPONSAVEL','Exigir documento do responsável','BOOLEAN','true',90),
('EDUCACAO','IDADE_POR_SERIE','Idade mínima e máxima por série','JSON','{}',100),('EDUCACAO','HABILITAR_PRE_MATRICULA','Habilitar pré-matrícula','BOOLEAN','true',110),
('EDUCACAO','HABILITAR_PORTAL_RESPONSAVEL','Habilitar portal do responsável','BOOLEAN','true',120),
('RH','EXIGIR_CPF','Exigir CPF','BOOLEAN','true',10),('RH','MATRICULA_FUNCIONAL_UNICA','Exigir matrícula funcional única','BOOLEAN','true',20),
('RH','EXIGIR_DADOS_BANCARIOS_FOLHA','Exigir dados bancários para folha','BOOLEAN','true',30),('RH','EXIGIR_CARGO_ATIVO','Exigir cargo ativo','BOOLEAN','true',40),
('RH','EXIGIR_LOTACAO_ATIVA','Exigir lotação ativa','BOOLEAN','true',50),('RH','EXIGIR_VINCULO_ATIVO','Exigir vínculo ativo','BOOLEAN','true',60),
('RH','PERMITIR_MULTIPLOS_VINCULOS','Permitir múltiplos vínculos','BOOLEAN','false',70),('RH','PRAZO_FERIAS_VENCIDAS_DIAS','Prazo de férias vencidas','INTEGER','365',80),
('RH','PRAZO_AFASTAMENTO_SEM_FIM_DIAS','Prazo de afastamento sem data fim','INTEGER','30',90),('RH','PERMITIR_PONTO_MANUAL','Permitir ponto manual','BOOLEAN','false',100),
('FOLHA','PERMITIR_COMPLEMENTAR','Permitir folha complementar','BOOLEAN','true',10),('FOLHA','BLOQUEAR_CALCULO_COM_CRITICA','Bloquear cálculo com crítica','BOOLEAN','true',20),
('FOLHA','PERMITIR_CRITICA_NAO_BLOQUEANTE','Permitir crítica não bloqueante','BOOLEAN','true',30),('FOLHA','EXIGIR_APROVACAO_FECHAMENTO','Exigir aprovação antes do fechamento','BOOLEAN','true',40),
('FOLHA','EXIGIR_JUSTIFICATIVA_REABRIR','Exigir justificativa para reabrir','BOOLEAN','true',50),('FOLHA','PERMITIR_LANCAMENTO_NEGATIVO','Permitir lançamento negativo','BOOLEAN','false',60),
('FOLHA','PERMITIR_SIMULACAO','Permitir simulação','BOOLEAN','true',70),('FOLHA','HABILITAR_INTEGRACAO_FINANCEIRA','Habilitar integração financeira','BOOLEAN','false',80),
('FOLHA','HABILITAR_REMESSA_CSV','Habilitar remessa CSV','BOOLEAN','true',90),
('PORTAL_SERVIDOR','HABILITADO','Habilitar portal do servidor','BOOLEAN','true',10),('PORTAL_EDUCACAO','HABILITADO','Habilitar portal da educação','BOOLEAN','true',10)
on conflict do nothing;

create or replace function sigov.salvar_parametro_modulo(p_tenant_id bigint,p_modulo varchar,p_codigo varchar,p_valor jsonb,p_usuario_id bigint,p_correlation_id varchar)
returns void language plpgsql as $function$
declare v_parametro_id bigint; v_anterior jsonb;
begin
 if p_tenant_id is null or p_tenant_id <= 0 then raise exception 'Tenant obrigatório'; end if;
 if nullif(trim(p_correlation_id),'') is null then raise exception 'CorrelationId obrigatório'; end if;
 select id into v_parametro_id from sigov.parametro_modulo where modulo=upper(p_modulo) and codigo=upper(p_codigo) and ativo and not is_deleted;
 if v_parametro_id is null then raise exception 'Parâmetro não encontrado: %.%',p_modulo,p_codigo; end if;
 select valor into v_anterior from sigov.parametro_modulo_valor where tenant_id=p_tenant_id and parametro_id=v_parametro_id and not is_deleted for update;
 insert into sigov.parametro_modulo_valor(tenant_id,parametro_id,valor,created_by,updated_by,correlation_id)
 values(p_tenant_id,v_parametro_id,p_valor,p_usuario_id,p_usuario_id,p_correlation_id)
 on conflict (tenant_id,parametro_id) where is_deleted=false do update set valor=excluded.valor,updated_at=now(),updated_by=p_usuario_id,correlation_id=p_correlation_id;
 insert into sigov.parametro_modulo_historico(tenant_id,parametro_id,valor_anterior,valor_novo,usuario_id,correlation_id,auditoria)
 values(p_tenant_id,v_parametro_id,v_anterior,p_valor,p_usuario_id,p_correlation_id,jsonb_build_object('antes',v_anterior,'depois',p_valor,'modulo',upper(p_modulo)));
end $function$;

-- Permissões granulares. O seed preserva perfis administrativos ao conceder todas as novas chaves.
do $permissions$
declare permission_key text;
begin
 foreach permission_key in array array[
 'educacao.dashboard.visualizar','educacao.escolas.visualizar','educacao.escolas.criar','educacao.escolas.editar','educacao.alunos.visualizar','educacao.alunos.criar','educacao.alunos.editar','educacao.alunos.dados_sensiveis.visualizar','educacao.matriculas.criar','educacao.matriculas.confirmar','educacao.matriculas.cancelar','educacao.matriculas.transferir','educacao.frequencia.lancar','educacao.frequencia.justificar','educacao.notas.lancar','educacao.boletim.visualizar','educacao.secretaria.documentos.emitir','educacao.diario.editar','educacao.conselho.aprovar','educacao.relatorios.visualizar',
 'rh.dashboard.visualizar','rh.servidores.visualizar','rh.servidores.criar','rh.servidores.editar','rh.servidores.dados_sensiveis.visualizar','rh.cargos.gerenciar','rh.lotacoes.gerenciar','rh.vinculos.gerenciar','rh.ponto.lancar','rh.ponto.homologar','rh.ferias.gerenciar','rh.afastamentos.gerenciar','rh.saude_ocupacional.visualizar','rh.portal.visualizar','rh.relatorios.visualizar',
 'folha.visualizar','folha.criar','folha.editar','folha.calcular','folha.simular','folha.conferir','folha.aprovar','folha.fechar','folha.reabrir','folha.cancelar','folha.eventos.gerenciar','folha.lancamentos.gerenciar','folha.contracheques.visualizar','folha.contracheques.todos.visualizar','folha.integracao_financeira.gerenciar','folha.remessa.gerar','folha.relatorios.visualizar']
 loop
  insert into sigov.permissao(chave,descricao,modulo,recurso,acao,ativo)
  values(permission_key,'Permissão granular RC50.29',split_part(permission_key,'.',1),split_part(permission_key,'.',2),split_part(permission_key,'.',3),true)
  on conflict (chave) do update set ativo=true,is_deleted=false;
 end loop;
 insert into sigov.perfil_permissao(tenant_id,perfil_acesso_id,permissao_id)
 select coalesce(pa.tenant_id,t.id),pa.id,p.id
 from sigov.perfil_acesso pa cross join sigov.tenant t join sigov.permissao p on p.descricao='Permissão granular RC50.29'
 where pa.ativo and not pa.is_deleted and t.ativo and not t.is_deleted
   and (upper(coalesce(pa.codigo_externo,'')) in ('ADMINISTRADOR_GERAL','ADMINISTRADOR_TENANT','SUPERADMIN') or upper(pa.nome) like '%ADMINISTRADOR%')
   and (pa.tenant_id is null or pa.tenant_id=t.id)
 on conflict do nothing;
end $permissions$;
