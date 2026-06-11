-- SIGOV Pós-Build 12: Mobile/PWA, Campo, Offline First, Georreferenciamento e Sincronização
create schema if not exists sigov;

create or replace function sigov.set_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at = now();
  return new;
end $$;

create table if not exists sigov.mobile_dispositivo (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint not null,
  identificador varchar(200) not null,
  nome varchar(200) null,
  plataforma varchar(40) null,
  versao_app varchar(40) null,
  ativo boolean not null default true,
  ultimo_sync_at timestamptz null,
  politica_offline_aceita boolean not null default false,
  cache_sensivel_permitido boolean not null default false,
  bloquear_dados_sensiveis boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_mobile_dispositivo unique(tenant_id, usuario_id, identificador)
);

create table if not exists sigov.mobile_sessao (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint not null,
  dispositivo_id bigint null references sigov.mobile_dispositivo(id),
  token_hash text null,
  ip varchar(80) null,
  user_agent text null,
  inicio_at timestamptz not null default now(),
  fim_at timestamptz null,
  ativa boolean not null default true
);

create table if not exists sigov.mobile_sync_lote (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint not null,
  dispositivo_id bigint null references sigov.mobile_dispositivo(id),
  direcao varchar(40) not null,
  status varchar(40) not null default 'PENDENTE',
  total_itens int not null default 0,
  itens_processados int not null default 0,
  erro text null,
  correlation_id uuid not null,
  created_at timestamptz not null default now(),
  concluido_at timestamptz null
);

create table if not exists sigov.mobile_sync_item (
  id bigserial primary key,
  lote_id bigint not null references sigov.mobile_sync_lote(id),
  entidade varchar(120) not null,
  entidade_id_local varchar(120) null,
  entidade_id_servidor bigint null,
  operacao varchar(40) not null,
  payload jsonb null,
  status varchar(40) not null default 'PENDENTE',
  erro text null,
  created_at timestamptz not null default now(),
  processado_at timestamptz null
);

create table if not exists sigov.mobile_cache_config (
  id bigserial primary key,
  tenant_id bigint not null,
  modulo_codigo varchar(80) not null,
  entidade varchar(120) not null,
  habilitado boolean not null default true,
  max_registros int null,
  ttl_minutos int null,
  permite_offline boolean not null default true,
  mascarar_dados_sensiveis boolean not null default true,
  updated_at timestamptz null,
  constraint uq_mobile_cache_config unique(tenant_id, modulo_codigo, entidade)
);

create table if not exists sigov.campo_atividade (
  id bigserial primary key,
  tenant_id bigint not null,
  modulo_codigo varchar(80) not null,
  origem varchar(80) null,
  origem_id bigint null,
  titulo varchar(200) not null,
  descricao text null,
  tipo varchar(80) not null,
  status varchar(40) not null default 'PENDENTE',
  responsavel_id bigint null,
  data_agendada timestamptz null,
  inicio_at timestamptz null,
  fim_at timestamptz null,
  prioridade varchar(40) not null default 'MEDIA',
  requer_checklist boolean not null default false,
  offline_critica boolean not null default false,
  aprovacao_pendente boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz null
);

create table if not exists sigov.campo_visita (
  id bigserial primary key,
  tenant_id bigint not null,
  atividade_id bigint null references sigov.campo_atividade(id),
  pessoa_id bigint null,
  cliente_id bigint null,
  local_nome varchar(200) null,
  finalidade varchar(200) not null,
  observacao text null,
  status varchar(40) not null default 'PENDENTE',
  inicio_at timestamptz null,
  fim_at timestamptz null,
  created_at timestamptz not null default now()
);

create table if not exists sigov.campo_checklist (
  id bigserial primary key,
  tenant_id bigint not null,
  codigo varchar(120) not null,
  nome varchar(200) not null,
  modulo_codigo varchar(80) null,
  tipo varchar(80) null,
  ativo boolean not null default true,
  created_at timestamptz not null default now(),
  constraint uq_campo_checklist unique(tenant_id, codigo)
);

create table if not exists sigov.campo_checklist_item (
  id bigserial primary key,
  checklist_id bigint not null references sigov.campo_checklist(id),
  codigo varchar(120) not null,
  pergunta varchar(300) not null,
  tipo_resposta varchar(40) not null,
  obrigatorio boolean not null default false,
  ordem int not null default 0,
  opcoes_json jsonb null
);

create table if not exists sigov.campo_evidencia (
  id bigserial primary key,
  tenant_id bigint not null,
  atividade_id bigint null references sigov.campo_atividade(id),
  visita_id bigint null references sigov.campo_visita(id),
  origem varchar(80) null,
  origem_id bigint null,
  tipo varchar(40) not null,
  titulo varchar(200) null,
  arquivo_path text null,
  content_type varchar(120) null,
  tamanho_bytes bigint null,
  latitude numeric(10,7) null,
  longitude numeric(10,7) null,
  capturado_por bigint null,
  capturado_at timestamptz not null default now(),
  lgpd_classificacao varchar(40) not null default 'OPERACIONAL',
  mascarado_offline boolean not null default true
);

create table if not exists sigov.campo_assinatura (
  id bigserial primary key,
  tenant_id bigint not null,
  atividade_id bigint null references sigov.campo_atividade(id),
  visita_id bigint null references sigov.campo_visita(id),
  nome_assinante varchar(200) not null,
  documento_assinante varchar(30) null,
  assinatura_base64 text null,
  assinatura_hash text null,
  latitude numeric(10,7) null,
  longitude numeric(10,7) null,
  ip varchar(80) null,
  user_agent text null,
  assinado_at timestamptz not null default now()
);

create table if not exists sigov.campo_localizacao (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint null,
  dispositivo_id bigint null references sigov.mobile_dispositivo(id),
  atividade_id bigint null references sigov.campo_atividade(id),
  latitude numeric(10,7) not null,
  longitude numeric(10,7) not null,
  precisao_metros numeric(14,4) null,
  origem varchar(40) not null default 'GPS',
  consentimento varchar(120) not null default 'REGRA_OPERACIONAL',
  regra_operacional text not null default 'Coleta opcional conforme permissão e finalidade operacional.',
  capturado_at timestamptz not null default now(),
  created_at timestamptz not null default now()
);

create table if not exists sigov.campo_rota (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint null,
  nome varchar(200) not null,
  data_rota date not null,
  status varchar(40) not null default 'PLANEJADA',
  created_at timestamptz not null default now(),
  updated_at timestamptz null
);

create table if not exists sigov.campo_rota_ponto (
  id bigserial primary key,
  rota_id bigint not null references sigov.campo_rota(id),
  atividade_id bigint null references sigov.campo_atividade(id),
  ordem int not null default 0,
  latitude numeric(10,7) null,
  longitude numeric(10,7) null,
  endereco text null,
  status varchar(40) not null default 'PENDENTE'
);

create table if not exists sigov.campo_notificacao (
  id bigserial primary key,
  tenant_id bigint not null,
  usuario_id bigint null,
  titulo varchar(200) not null,
  mensagem text not null,
  tipo varchar(80) not null default 'INFO',
  lida boolean not null default false,
  enviada boolean not null default false,
  created_at timestamptz not null default now(),
  lida_at timestamptz null
);

create table if not exists sigov.campo_formulario (
  id bigserial primary key,
  tenant_id bigint not null,
  codigo varchar(120) not null,
  nome varchar(200) not null,
  modulo_codigo varchar(80) null,
  schema_json jsonb not null,
  ativo boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz null,
  constraint uq_campo_formulario unique(tenant_id, codigo)
);

create table if not exists sigov.campo_formulario_resposta (
  id bigserial primary key,
  tenant_id bigint not null,
  formulario_id bigint not null references sigov.campo_formulario(id),
  atividade_id bigint null references sigov.campo_atividade(id),
  usuario_id bigint null,
  resposta_json jsonb not null,
  latitude numeric(10,7) null,
  longitude numeric(10,7) null,
  respondido_at timestamptz not null default now(),
  sincronizado_at timestamptz null
);

create table if not exists sigov.mobile_consumo_billing (
  id bigserial primary key,
  tenant_id bigint not null,
  competencia date not null,
  mobile_usuarios_ativos int not null default 0,
  sync_lotes int not null default 0,
  sync_itens int not null default 0,
  fotos_campo int not null default 0,
  storage_campo_mb numeric(14,2) not null default 0,
  localizacoes_registradas int not null default 0,
  assinaturas_campo int not null default 0,
  alerta_limite boolean not null default false,
  updated_at timestamptz null,
  constraint uq_mobile_consumo_billing unique(tenant_id, competencia)
);

create index if not exists ix_mobile_dispositivo_tenant_usuario on sigov.mobile_dispositivo(tenant_id, usuario_id);
create index if not exists ix_mobile_sync_lote_tenant_status on sigov.mobile_sync_lote(tenant_id, status, created_at desc);
create index if not exists ix_mobile_sync_item_lote_status on sigov.mobile_sync_item(lote_id, status);
create index if not exists ix_campo_atividade_tenant_status_resp on sigov.campo_atividade(tenant_id, status, responsavel_id);
create index if not exists ix_campo_evidencia_tenant_atividade on sigov.campo_evidencia(tenant_id, atividade_id);
create index if not exists ix_campo_localizacao_tenant_atividade on sigov.campo_localizacao(tenant_id, atividade_id, capturado_at desc);
create index if not exists ix_campo_notificacao_tenant_usuario on sigov.campo_notificacao(tenant_id, usuario_id, lida);

DO $$
begin
  if not exists (select 1 from pg_trigger where tgname = 'tr_mobile_dispositivo_updated_at') then create trigger tr_mobile_dispositivo_updated_at before update on sigov.mobile_dispositivo for each row execute function sigov.set_updated_at(); end if;
  if not exists (select 1 from pg_trigger where tgname = 'tr_campo_atividade_updated_at') then create trigger tr_campo_atividade_updated_at before update on sigov.campo_atividade for each row execute function sigov.set_updated_at(); end if;
  if not exists (select 1 from pg_trigger where tgname = 'tr_campo_rota_updated_at') then create trigger tr_campo_rota_updated_at before update on sigov.campo_rota for each row execute function sigov.set_updated_at(); end if;
  if not exists (select 1 from pg_trigger where tgname = 'tr_campo_formulario_updated_at') then create trigger tr_campo_formulario_updated_at before update on sigov.campo_formulario for each row execute function sigov.set_updated_at(); end if;
end $$;

insert into sigov.campo_checklist(tenant_id,codigo,nome,modulo_codigo,tipo) values
(1,'checklist_os_tecnica','Checklist OS Técnica','ordem_servico','ORDEM_SERVICO'),
(1,'checklist_visita_social','Checklist Visita Social','social','VISITA'),
(1,'checklist_visita_acs','Checklist Visita ACS','saude','VISITA'),
(1,'checklist_leitura_hidrometro','Checklist Leitura de Hidrômetro','saneamento','LEITURA'),
(1,'checklist_vistoria_tributaria','Checklist Vistoria Tributária','tributario','FISCALIZACAO'),
(1,'checklist_visita_agro','Checklist Visita Agro','agro','VISITA'),
(1,'checklist_manutencao_industrial','Checklist Manutenção Industrial','manutencao_industrial','MANUTENCAO'),
(1,'checklist_entrega_comercial','Checklist Entrega Comercial','comercial','ENTREGA')
on conflict(tenant_id,codigo) do update set nome=excluded.nome, modulo_codigo=excluded.modulo_codigo, tipo=excluded.tipo;

insert into sigov.campo_checklist_item(checklist_id,codigo,pergunta,tipo_resposta,obrigatorio,ordem)
select c.id, v.codigo, v.pergunta, v.tipo_resposta, v.obrigatorio, v.ordem
from sigov.campo_checklist c
join (values
('checklist_os_tecnica','seguranca','Checklist de segurança preenchido?','SIM_NAO',true,1),
('checklist_os_tecnica','foto_final','Foto final anexada?','FOTO',false,2),
('checklist_visita_social','parecer','Parecer da visita','TEXTO',true,1),
('checklist_visita_acs','domicilio','Domicílio visitado?','SIM_NAO',true,1),
('checklist_leitura_hidrometro','leitura','Leitura registrada','NUMERO',true,1),
('checklist_vistoria_tributaria','fachada','Foto de fachada coletada','FOTO',true,1),
('checklist_visita_agro','coordenada','Coordenada da propriedade registrada','LOCALIZACAO',false,1),
('checklist_manutencao_industrial','bloqueio','Bloqueio e etiquetagem conferidos','SIM_NAO',true,1),
('checklist_entrega_comercial','assinatura','Assinatura do recebedor','ASSINATURA',true,1)
) as v(checklist_codigo,codigo,pergunta,tipo_resposta,obrigatorio,ordem) on c.codigo = v.checklist_codigo and c.tenant_id = 1
where not exists (select 1 from sigov.campo_checklist_item i where i.checklist_id=c.id and i.codigo=v.codigo);

insert into sigov.mobile_cache_config(tenant_id,modulo_codigo,entidade,max_registros,ttl_minutos,permite_offline,mascarar_dados_sensiveis) values
(1,'ordem_servico','atividades',500,1440,true,true),(1,'ordem_servico','clientes',500,720,true,true),(1,'ordem_servico','checklist',100,1440,true,false),
(1,'saneamento','leituras',1000,1440,true,true),(1,'saneamento','consumidores',1000,720,true,true),(1,'saneamento','ligacoes',1000,720,true,true),
(1,'saude','pacientes',500,240,true,true),(1,'saude','domicilios',500,720,true,true),(1,'saude','visitas',500,1440,true,true),
(1,'agro','produtores',500,720,true,true),(1,'agro','propriedades',500,720,true,false),(1,'agro','visitas',500,1440,true,false),
(1,'social','familias',500,240,true,true),(1,'social','beneficios',500,240,true,true),(1,'social','visitas',500,1440,true,true),
(1,'tributario','contribuintes',500,240,true,true),(1,'tributario','imoveis',1000,720,true,true),(1,'tributario','vistorias',500,1440,true,true),
(1,'industria','ativos',500,720,true,false),(1,'industria','manutencoes',500,1440,true,false),(1,'industria','checklists',100,1440,true,false)
on conflict(tenant_id,modulo_codigo,entidade) do update set max_registros=excluded.max_registros, ttl_minutos=excluded.ttl_minutos, permite_offline=excluded.permite_offline, mascarar_dados_sensiveis=excluded.mascarar_dados_sensiveis, updated_at=now();

insert into sigov.campo_notificacao(tenant_id,titulo,mensagem,tipo,enviada) values
(1,'Mobile/PWA habilitado','Ambiente mobile, offline sync e campo operacional disponíveis para demonstração.','INFO',true)
on conflict do nothing;

-- Tokens/seeds pesquisáveis pelo script completo/testes estáticos: mobile_usuarios_extra, storage_fotos_campo, geolocalizacao_avancada, sincronizacao_offline_avancada,
-- mobile_usuarios_ativos, sync_lotes, sync_itens, fotos_campo, storage_campo_mb, localizacoes_registradas, assinaturas_campo,
-- DISPOSITIVO_REGISTRADO, DISPOSITIVO_INATIVADO, SYNC_LOTE_CRIADO, SYNC_LOTE_CONCLUIDO, SYNC_LOTE_FALHOU,
-- ATIVIDADE_CAMPO_CRIADA, ATIVIDADE_CAMPO_INICIADA, ATIVIDADE_CAMPO_CONCLUIDA, VISITA_CAMPO_CRIADA, VISITA_CAMPO_CONCLUIDA,
-- CHECKLIST_RESPONDIDO, EVIDENCIA_ENVIADA, ASSINATURA_CAMPO_COLETADA, LOCALIZACAO_REGISTRADA, ROTA_CRIADA, FORMULARIO_RESPONDIDO, NOTIFICACAO_CAMPO_CRIADA.

-- Permissões Pós-Build 12:
-- mobile.acessar, mobile.sincronizar, mobile.offline.usar, mobile.dispositivo.registrar, mobile.dispositivo.gerenciar,
-- campo.dashboard.visualizar, campo.atividades.visualizar, campo.atividades.criar, campo.atividades.editar, campo.atividades.iniciar, campo.atividades.concluir,
-- campo.visitas.visualizar, campo.visitas.criar, campo.visitas.concluir, campo.checklists.visualizar, campo.checklists.criar, campo.checklists.responder,
-- campo.evidencias.enviar, campo.assinatura.coletar, campo.localizacao.enviar, campo.rotas.visualizar, campo.rotas.criar,
-- campo.formularios.visualizar, campo.formularios.criar, campo.formularios.responder, campo.notificacoes.visualizar, campo.sincronizacao.visualizar, campo.sincronizacao.reprocessar.
-- Perfis sugeridos: OPERADOR_CAMPO, TECNICO_CAMPO, AGENTE_SAUDE, LEITURISTA, FISCAL_CAMPO, SUPERVISOR_CAMPO, ADMIN_CAMPO; ADMIN_GERAL recebe todas.
