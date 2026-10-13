-- RC-EVO Ciclo 2 Sprint 8 (Compras integradas 1/2: demanda -> contratacao -> contrato) -
-- incrementos BR-8.1 a BR-8.7 sobre os contratos BR-7.x da S7 (docs/CONTRATOS-FRENTE-ADMINISTRATIVA-
-- C2S7-MULTIESFERA.md). Legislagao: Lei 14.133/2021 (arts. 6, 18-29, 30-57, 75, 100-116, 106, 107 §4,
-- 128, 130), LC 123/2006 (zona ME/EPP). Idempotente (regra 6); PK bigint identity (regra 4);
-- multi-esfera (regras 21-24): chaves por tenant x entidade, catalogo de modalidades com esfera
-- NULL = aplica-se a todas as esferas e todos os tetos/prazos/percentuais sao parametros do banco
-- (regra 11), nada municipal hardcoded. Sem alteracao das tabelas legadas existentes (regra 5):
-- apenas add column if not exists + tabelas novas + backstops de unicidade dos comandos tipados.
--
-- Estados canonicos da frente integrada (gravados pelos comandos tipados):
--   demanda.status: SOLICITADA | APROVADA_PARCIAL (urgencia aguardando dupla aprovacao) |
--                   APROVADA | REPELIDA (justificativa) | CANCELADA (so antes da aprovacao)
--   licitacao.status: ABERTA (recebendo lances) | JULGADA | ADJUDICADA | HOMOLOGADA
--   licitacao.zona_me_epp_status: NAO_APLICAVEL | ABERTA | ENCERRADA_POR_PREFERENCIA |
--                                 ENCERRADA_FORA_MARGEM (desistencia documentada)
--   habilitacao.status: APROVADO | REPROVADO (por tipo JURIDICO|FISCAL_TRABALHISTA|TECNICO)
--   proposta.status: CLASSIFICADA | VENCEDORA | REJEITADA_INEXEQUIVEL
--   contrato.status: VIGENTE | RESCIDO (prorrogacao mantem VIGENTE com rastro em historico)

-- 1) Demanda tipada (BR-8.1): urgencia com justificativa expandida e trilha da dupla aprovacao
--    (ordenador + autoridade superior; separacao de funcoes: solicitante != aprovador); centro de
--    custo valido e ativo referenciado por item; aprovada e imutavel (alteracao so por nova versao,
--    S8 documenta o bloqueio DEMANDA_APOS_APROVACAO_IMUTAVEL nos comandos).
alter table sigov.compras_demanda add column if not exists urgencia boolean not null default false;
alter table sigov.compras_demanda add column if not exists justificativa_urgencia text;
alter table sigov.compras_demanda add column if not exists centro_custo_id bigint;
alter table sigov.compras_demanda add column if not exists aprovado_em timestamptz;
alter table sigov.compras_demanda add column if not exists aprovado_por bigint;
alter table sigov.compras_demanda add column if not exists dupla_aprovado_em timestamptz;
alter table sigov.compras_demanda add column if not exists dupla_aprovado_por bigint;

-- 1b) Relaxamento nao-destrutivo dos dois checks de enum LEGADOS da tabela (mesmo padrao da
--     secao 12 desta migration sobre compras_modalidade_config): a geracao integrada da S8
--     grava estados MAIUSCULOS (SOLICITADA|APROVADA_PARCIAL|APROVADA|REPELIDA|CANCELADA e
--     prioridade NORMAL) que nao existem no dominio legado minusculo; as checks passam a
--     aceitar as duas geracoes e todos os dados legados existentes permanecem validos intactos
--     (comparacao case-sensitive = dominios independentes compartilhando a coluna).
alter table sigov.compras_demanda drop constraint if exists compras_demanda_status_check;
alter table sigov.compras_demanda add constraint compras_demanda_status_check check ((status)::text = any (array['rascunho','submetida','aprovada','reprovada','cancelada','SOLICITADA','APROVADA_PARCIAL','APROVADA','REPELIDA','CANCELADA']));
alter table sigov.compras_demanda drop constraint if exists compras_demanda_prioridade_check;
alter table sigov.compras_demanda add constraint compras_demanda_prioridade_check check ((prioridade)::text = any (array['baixa','normal','alta','urgente','BAIXA','NORMAL','ALTA','URGENTE']));

-- 1c) Demais checks de enum LEGADAS compartilhadas com a frente integrada (mesmo padrao das
--     secoes 1b e 12): a geracao S8 grava estados canonicos que nao existem nos dominios
--     legados; cada check passa a aceitar as duas geracoes sem tocar em dado legado existente.
--       licitacao.status: S8 ABERTA|JULGADA|ADJUDICADA|HOMOLOGADA (sessao eletronica BR-8.2/8.4);
--       processo.status: S8 ABERTO (abertura tipada da BR-8.2);
--       habilitacao.status: S8 APROVADO|REPROVADO (+ ANULADO|CANCELADO referidos no indice unico);
--       proposta.status: S8 CLASSIFICADA|VENCEDORA|REJEITADA_INEXEQUIVEL (BR-8.4);
--       contrato.status: S8 RESCIDO (ato de rescisao BR-8.5);
--       proposta.vinculo: a proposta S8 e de nivel FORNECEDOR da sessao (sem vinculo legado a
--       lote/item) - o XOR legado e relaxado para "proibido duplo vinculo" (ambos podem ser NULL).
alter table sigov.compras_licitacao drop constraint if exists compras_licitacao_status_check;
alter table sigov.compras_licitacao add constraint compras_licitacao_status_check check ((status)::text = any (array['rascunho','em_planejamento','publicado','em_disputa','julgamento','habilitacao','recurso','adjudicado','homologado','fracassado','deserto','revogado','anulado','cancelado','ABERTA','JULGADA','ADJUDICADA','HOMOLOGADA']));
alter table sigov.compras_processo drop constraint if exists ck_compras_processo_status;
alter table sigov.compras_processo add constraint ck_compras_processo_status check ((status)::text = any (array['PLANEJAMENTO','PESQUISA_PRECO','EDITAL_TR','PUBLICADO','RECEBENDO_PROPOSTAS','JULGAMENTO','ADJUDICADO','HOMOLOGADO','CONTRATADO','FRACASSADO','DESERTO','CANCELADO','ABERTO']));
alter table sigov.compras_habilitacao drop constraint if exists compras_habilitacao_status_check;
alter table sigov.compras_habilitacao add constraint compras_habilitacao_status_check check ((status)::text = any (array['pendente','habilitado','inabilitado','APROVADO','REPROVADO','ANULADO','CANCELADO']));
alter table sigov.compras_proposta drop constraint if exists compras_proposta_status_check;
alter table sigov.compras_proposta add constraint compras_proposta_status_check check ((status)::text = any (array['recebida','classificada','desclassificada','vencedora','CLASSIFICADA','VENCEDORA','REJEITADA_INEXEQUIVEL']));
-- Alargamento corretivo (mesma RC, mesmo padrao da zona): REJEITADA_INEXEQUIVEL tem 21 chars e
-- estourava o varchar(20) legado do status na rejeicao por inexequibilidade (22001 na rodada 13).
-- Idempotente; cobre tambem a base ja criada com varchar(20).
alter table sigov.compras_proposta alter column status type varchar(40);
alter table sigov.compras_contrato drop constraint if exists ck_compras_contrato_status;
alter table sigov.compras_contrato add constraint ck_compras_contrato_status check ((status)::text = any (array['VIGENTE','SUSPENSO','ENCERRADO','RESCINDIDO','VENCIDO','RESCIDO']));
alter table sigov.compras_proposta drop constraint if exists compras_proposta_check;
alter table sigov.compras_proposta add constraint compras_proposta_check check (not (lote_id is not null and item_id is not null));

-- 2) Itens da demanda (BR-8.1): 1 demanda -> N itens; qtd > 0, unidade conhecida, preco estimado > 0
--    ou pesquisa de precos referenciada; 1 sequencia por demanda (backstop transacional).
create table if not exists sigov.compras_demanda_item(id bigint generated by default as identity primary key,tenant_id bigint not null,entidade_id bigint,esfera_governo varchar(20),exercicio_id bigint,demanda_id bigint not null,sequencia integer not null,descricao text not null,quantidade numeric(18,3) not null,unidade varchar(20) not null,tipo_objeto varchar(20) not null default 'BEM',preco_estimado numeric(18,2),pesquisa_preco_id bigint,centro_custo_id bigint,auditoria jsonb not null default '[]'::jsonb,correlation_id uuid not null default gen_random_uuid(),ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now(),created_by bigint);
create unique index if not exists ux_compras_demanda_item_tenant_demanda_seq on sigov.compras_demanda_item(tenant_id, demanda_id, sequencia) where is_deleted=false and ativo=true;

-- 3) Processo unico da contratacao (BR-8.2, Lei 14.133 art. 128): demanda APROVADA abre/vincula um
--    unico processo vigente que agrega todos os atos; 1 processo vigente por demanda (posicoes
--    terminais liberam a unicidade). modalidade_id fica nullable: a modalidade e definida na
--    abertura do procedimento (nada predefinido em codigo).
alter table sigov.compras_processo add column if not exists demanda_id bigint;
alter table sigov.compras_processo alter column modalidade_id drop not null;
alter table sigov.compras_processo alter column criterio_id drop not null;
create unique index if not exists ux_compras_processo_tenant_demanda_vigente on sigov.compras_processo(tenant_id, demanda_id) where demanda_id is not null and status not in ('CANCELADO','FRACASSADO','DESERTO');

-- 4) Fornecedor: classificacao ME/EPP (LC 123/2006) - coluna nullable (legado preservado;
--    OUTROS/null = fora da zona de preferencias).
alter table sigov.compras_fornecedor add column if not exists categoria_me_epp varchar(10);

-- 5) Procedimento/licitacao (BR-8.3): estado da zona de preferencias ME/EPP e a documentacao do
--    desfecho (preferencia exercida ou desistencia fora da margem) gravados no proprio registro.
alter table sigov.compras_licitacao add column if not exists zona_me_epp_status varchar(40) not null default 'NAO_APLICAVEL';
-- Alargamento corretivo (mesma RC): os estados canonicos ENCERRADA_POR_PREFERENCIA (23 chars) e
-- ENCERRADA_FORA_MARGEM (21) excediam o tamanho inicialmente previsto de 20 (22001 na rodada 9).
-- Idempotente; cobre tambem a base onde a coluna ja existe com varchar(20).
alter table sigov.compras_licitacao alter column zona_me_epp_status type varchar(40);
alter table sigov.compras_licitacao add column if not exists zona_motivo text;

-- 6) Lances (BR-8.3): 1 lance = 1 registro; backstop unico por licitacao x fornecedor x sequencia
--    (idempotencia por autor x sequencia; reenvio com mesma chave volta o efeito original no
--    comando tipado, chave gravada na auditoria).
create table if not exists sigov.compras_lance(id bigint generated by default as identity primary key,tenant_id bigint not null,entidade_id bigint,licitacao_id bigint not null,fornecedor_id bigint not null,sequencia integer not null,valor numeric(18,2) not null,chave_idempotencia varchar(80),auditoria jsonb not null default '[]'::jsonb,correlation_id uuid not null default gen_random_uuid(),ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now(),created_by bigint);
create unique index if not exists ux_compras_lance_tenant_licitacao_forn_seq on sigov.compras_lance(tenant_id, licitacao_id, fornecedor_id, sequencia) where is_deleted=false and ativo=true;
create index if not exists ix_compras_lance_tenant_licitacao on sigov.compras_lance(tenant_id, licitacao_id, valor);

-- 7) Habilitacao por tipo (BR-8.3): qualificacao juridica/fiscal-trabalhista/tecnica julgada como
--    ato proprio; 1 julgamento vigente por licitacao x fornecedor x tipo.
alter table sigov.compras_habilitacao add column if not exists tipo varchar(20);
create unique index if not exists ux_compras_habilitacao_tenant_licitacao_forn_tipo on sigov.compras_habilitacao(tenant_id, licitacao_id, fornecedor_id, tipo) where tipo is not null and status not in ('ANULADO','CANCELADO');

-- 8) Julgamento/adjudicacao (BR-8.4): classificacao automatica grava ordem_classificacao nas
--    propostas (tabela existente); adjudicacao ganha valor total derivado e itens espelhando os
--    adjudicados (proposta vencedora x itens da licitacao).
alter table sigov.compras_adjudicacao add column if not exists valor_total numeric(18,2);
create table if not exists sigov.compras_adjudicacao_item(id bigint generated by default as identity primary key,tenant_id bigint not null,entidade_id bigint,adjudicacao_id bigint not null,licitacao_item_id bigint,sequencia integer not null,descricao text not null,quantidade numeric(18,3) not null,valor_unitario numeric(18,2) not null,total numeric(18,2) not null,auditoria jsonb not null default '[]'::jsonb,correlation_id uuid not null default gen_random_uuid(),ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now(),created_by bigint);
create unique index if not exists ux_compras_adjudicacao_item_tenant_adj_seq on sigov.compras_adjudicacao_item(tenant_id, adjudicacao_id, sequencia) where is_deleted=false and ativo=true;

-- 9) Contrato nasce de homologacao (BR-8.5): vinculo com unica homologacao vigente (1 contrato
--    vigente x procedimento), data de eficacia e modo de assinatura (GED quando habilitado, senao
--    fisico); itens espelham os adjudicados (valor unitario x quantidade maxima) e o valor total e
--    DERIVADO (soma dos itens) - nao editavel livremente nos comandos.
alter table sigov.compras_contrato add column if not exists homologacao_id bigint;
alter table sigov.compras_contrato add column if not exists data_eficacia date;
alter table sigov.compras_contrato add column if not exists assinatura_modo varchar(10);
create unique index if not exists ux_compras_contrato_tenant_homologacao_vigente on sigov.compras_contrato(tenant_id, homologacao_id) where homologacao_id is not null and status in ('VIGENTE','PRORROGADO');
create table if not exists sigov.compras_contrato_item(id bigint generated by default as identity primary key,tenant_id bigint not null,entidade_id bigint,contrato_id bigint not null,sequencia integer not null,descricao text not null,quantidade_maxima numeric(18,3) not null,valor_unitario numeric(18,2) not null,total numeric(18,2) not null,auditoria jsonb not null default '[]'::jsonb,correlation_id uuid not null default gen_random_uuid(),ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now(),created_by bigint);
create unique index if not exists ux_compras_contrato_item_tenant_contrato_seq on sigov.compras_contrato_item(tenant_id, contrato_id, sequencia) where is_deleted=false and ativo=true;

-- 10) Dispensa de previa (BR-8.6, Lei 14.133 art. 75): base legal SELECIONADA DO CATALOGO (banco =
--     autoridade, regra 11/12) - sem texto livre; catalogo canonico idempotente.
create table if not exists sigov.compras_dispensa_base_legal(id bigint generated by default as identity primary key,codigo varchar(60) not null,nome text not null,lei varchar(40) not null,artigo varchar(40) not null,auditoria jsonb not null default '[]'::jsonb,ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now());
create unique index if not exists ux_compras_dispensa_base_legal_codigo on sigov.compras_dispensa_base_legal(codigo) where is_deleted=false and ativo=true;
insert into sigov.compras_dispensa_base_legal(codigo,nome,lei,artigo) values
 ('ART_75_I','Emergencia ou calamidade publica (urgencia ou emergencia)','Lei 14.133/2021','art. 75, I'),
 ('ART_75_II','Pequeno valor, nos limites vigentes para compras e servicos comuns','Lei 14.133/2021','art. 75, II'),
 ('ART_75_III','Servicos de natureza singular, com profissionais ou firmas de reputacao incomum','Lei 14.133/2021','art. 75, III'),
 ('ART_75_VI','Residuos de obras, servicos ou compras, ate o limite legal','Lei 14.133/2021','art. 75, VI'),
 ('ART_75_XI','Falha ou frustracao de licitacao anterior','Lei 14.133/2021','art. 75, XI')
on conflict do nothing;

-- 11) Parametros canonicos da frente (contratos S7 secao 5 - tudo parametrizavel, nada hardcoded):
--     margem ME/EPP (LC 123/2006, default 5%), prazo de homologacao, teto de consulta previa
--     (valor vigente na data de homologacao - atualizar via parametro quando a lei mudar),
--     teto de aditivo quantitativo de obras (art. 107 §4), prazo de substituicao de rejeicao
--     (S9), teto/prorrogações de contrato (art. 106) e teto de execucao (S9). Sobrescrita por
--     tenant via parametro_modulo_valor (mesmo padrao EDUCACAO).
insert into sigov.parametro_modulo(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo) values
 ('COMPRAS','LICITACAO_MARGEM_ME_EPP_PCT','Margem de preferencia ME/EPP (pct)','BR-8.3: margem da zona de preferencias de tratamento diferenciado (LC 123/2006, default 5%)','INTEGER','5',false,220,true),
 ('COMPRAS','HOMOLOGACAO_PRAZO_MAX_DIAS','Prazo maximo de homologacao (dias)','BR-8.4: homologacao deve ocorrer dentro do prazo parametrico contado da adjudicacao','INTEGER','30',false,221,true),
 ('COMPRAS','COMPRAS_CONSULTA_PREVIA_TETO','Teto de valor para consulta previa','BR-8.3: consulta previa somente ate o teto vigente (Lei 14.133 art. 70, II); valor parametrizavel por atualizacao legal','DECIMAL','118704.36',true,222,true),
 ('COMPRAS','ADITIVO_TETO_QUANTITATIVO_PCT_OBRA','Teto de aditivo quantitativo - obras (pct)','BR-7.4/S10: acrescimo quantitativo em obras limitado (art. 107 §4, default 50%); bens/servicos por parametro proprio quando criado','INTEGER','50',false,223,true),
 ('COMPRAS','RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS','Prazo de substituicao de rejeicao (dias uteis)','BR-7.5/S9: rejeicao com prazo de substituicao parametrico (default 5 dias uteis)','INTEGER','5',false,224,true),
 ('COMPRAS','PRORROGACAO_TETO_ANOS','Teto de prorrogacao de contrato (anos)','BR-8.5: prorrogacao limitada (Lei 14.133 art. 106, default 5 anos)','INTEGER','5',false,225,true),
 ('COMPRAS','PRORROGACAO_MAX_VEZES','Maximo de prorrogacoes de contrato (vezes)','BR-8.5: maximo de prorrogacoes (art. 106, default 5 vezes)','INTEGER','5',false,226,true),
 ('COMPRAS','CONTRATO_EXECUCAO_TETO_PCT','Teto de execucao contratual (pct)','BR-8.5/S9: execucao percentual que bloqueia novo fornecimento (CONTRATO_EXECUCAO_TETO; default 100%)','INTEGER','100',false,227,true)
on conflict do nothing;

-- 12) Catalogo de modalidades do procedimento (RC50.85/14.133): seed canonica idempotente por
--     tenant ativo, esfera_governo NULL = aplica-se a todas as esferas (regra 24); a tabela
--     existente estava vazia em homologacao. Unicidade restrita a linhas globais (entidade_id
--     null) para nao travar configuracoes legadas por entidade.
create unique index if not exists ux_compras_modalidade_config_tenant_mod_esfera on sigov.compras_modalidade_config(tenant_id, modalidade, coalesce(esfera_governo,'')) where ativo=true and entidade_id is null;
-- entidade_id/esfera_governo ficam nullable: linha global (entidade/esfera null) = catalogo
-- canonico do tenant aplicavel a todas as entidades e esferas (regra 24). As checks de enum
-- legadas (RC50.85) predizem o vocabulario da Lei 14.133/2021 (ex.: CONSULTA_PREVIA ausente,
-- valores minusculos); sao RELAXADAS (nao-destrutivo - dados existentes preservados intactos) e
-- a validacao passa a ser do catalogo (banco = autoridade, regras 11/12) + comandos tipados.
alter table sigov.compras_modalidade_config alter column entidade_id drop not null;
alter table sigov.compras_modalidade_config alter column esfera_governo drop not null;
alter table sigov.compras_modalidade_config drop constraint if exists compras_modalidade_config_modalidade_check;
alter table sigov.compras_modalidade_config drop constraint if exists compras_modalidade_config_criterio_julgamento_check;
alter table sigov.compras_modalidade_config drop constraint if exists compras_modalidade_config_esfera_governo_check;
insert into sigov.compras_modalidade_config(tenant_id,entidade_id,esfera_governo,modalidade,criterio_julgamento,exige_dfd,exige_etp,exige_mapa_riscos,exige_termo_referencia,ativo,created_by,updated_by)
select t.id,null,null,m.mod,m.crit,m.dfd,m.etp,m.riscos,m.termo,true,0,0
from (select distinct id from sigov.tenant where ativo=true and is_deleted=false) t
cross join (values
 ('PREGAO_ELETRONICO','MENOR_PRECO',false,false,false,true),
 ('CONCORRENCIA','TECNICA_PRECO',true,true,true,true),
 ('CONCURSO_PUBLICO','TECNICA_PRECO',false,true,false,true),
 ('DILOGO_ELETRONICO','MENOR_PRECO',false,true,true,true),
 ('PRE_QUALIFICACAO','TECNICA_PRECO',true,false,false,false),
 ('MANIFESTACAO_DE_INTERESSE','TECNICA_PRECO',true,true,true,true),
 ('PARCERIA_PRIVADA','MAIOR_DESCONTO',true,true,true,true),
 ('CONSULTA_PREVIA','MENOR_PRECO',false,false,false,false),
 ('DISPENSA','MENOR_PRECO',false,false,false,true),
 ('INEXIGIBILIDADE','TECNICA_PRECO',false,false,false,true)
) as m(mod,crit,dfd,etp,riscos,termo)
where not exists(select 1 from sigov.compras_modalidade_config c where c.tenant_id=t.id and c.modalidade=m.mod and c.esfera_governo is null and c.entidade_id is null and c.ativo=true);

-- C2S8-POSTCOND: falha explicita se faltar qualquer artefato do incremento (regra 13)
do $$
declare
  n_tenants bigint;
  n_mod_tenants bigint;
  idx text;
begin
  if not exists(select 1 from information_schema.tables where table_schema='sigov' and table_name='compras_demanda_item') then
    raise exception 'C2S8-POSTCOND: tabela sigov.compras_demanda_item ausente';
  end if;
  if not exists(select 1 from information_schema.tables where table_schema='sigov' and table_name='compras_lance') then
    raise exception 'C2S8-POSTCOND: tabela sigov.compras_lance ausente';
  end if;
  if not exists(select 1 from information_schema.tables where table_schema='sigov' and table_name='compras_adjudicacao_item') then
    raise exception 'C2S8-POSTCOND: tabela sigov.compras_adjudicacao_item ausente';
  end if;
  if not exists(select 1 from information_schema.tables where table_schema='sigov' and table_name='compras_contrato_item') then
    raise exception 'C2S8-POSTCOND: tabela sigov.compras_contrato_item ausente';
  end if;
  if not exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_processo' and column_name='demanda_id') then
    raise exception 'C2S8-POSTCOND: coluna sigov.compras_processo.demanda_id ausente';
  end if;
  if not exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_contrato' and column_name='homologacao_id') then
    raise exception 'C2S8-POSTCOND: coluna sigov.compras_contrato.homologacao_id ausente';
  end if;
  foreach idx in array array['ux_compras_demanda_item_tenant_demanda_seq','ux_compras_lance_tenant_licitacao_forn_seq','ux_compras_habilitacao_tenant_licitacao_forn_tipo','ux_compras_adjudicacao_item_tenant_adj_seq','ux_compras_contrato_tenant_homologacao_vigente','ux_compras_contrato_item_tenant_contrato_seq','ux_compras_processo_tenant_demanda_vigente','ux_compras_dispensa_base_legal_codigo','ux_compras_modalidade_config_tenant_mod_esfera'] loop
    if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname=idx) then
      raise exception 'C2S8-POSTCOND: indice % ausente', idx;
    end if;
  end loop;
  if (select count(1) from sigov.compras_dispensa_base_legal where ativo and not is_deleted)<5 then
    raise exception 'C2S8-POSTCOND: catalogo de dispensas (art. 75) incompleto';
  end if;
  if (select count(1) from sigov.parametro_modulo where modulo='COMPRAS' and ativo and not is_deleted and codigo in ('LICITACAO_MARGEM_ME_EPP_PCT','HOMOLOGACAO_PRAZO_MAX_DIAS','COMPRAS_CONSULTA_PREVIA_TETO','ADITIVO_TETO_QUANTITATIVO_PCT_OBRA','RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS','PRORROGACAO_TETO_ANOS','PRORROGACAO_MAX_VEZES','CONTRATO_EXECUCAO_TETO_PCT'))<>8 then
    raise exception 'C2S8-POSTCOND: parametros canonicos COMPRAS incompletos no catalogo';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_demanda' and con.conname='compras_demanda_status_check' and pg_get_constraintdef(con.oid) like '%SOLICITADA%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status da demanda (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_demanda' and con.conname='compras_demanda_prioridade_check' and pg_get_constraintdef(con.oid) like '%NORMAL%') then
    raise exception 'C2S8-POSTCOND: check relaxado de prioridade da demanda (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_licitacao' and con.conname='compras_licitacao_status_check' and pg_get_constraintdef(con.oid) like '%HOMOLOGADA%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status da licitacao (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_processo' and con.conname='ck_compras_processo_status' and pg_get_constraintdef(con.oid) like '%ABERTO%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status do processo (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_habilitacao' and con.conname='compras_habilitacao_status_check' and pg_get_constraintdef(con.oid) like '%REPROVADO%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status da habilitacao (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_proposta' and con.conname='compras_proposta_status_check' and pg_get_constraintdef(con.oid) like '%REJEITADA_INEXEQUIVEL%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status da proposta (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_contrato' and con.conname='ck_compras_contrato_status' and pg_get_constraintdef(con.oid) like '%RESCIDO%') then
    raise exception 'C2S8-POSTCOND: check relaxado de status do contrato (geracao S8) ausente';
  end if;
  if not exists(select 1 from pg_constraint con join pg_class c on c.oid=con.conrelid join pg_namespace nsp on nsp.oid=c.relnamespace where nsp.nspname='sigov' and c.relname='compras_proposta' and con.conname='compras_proposta_check' and pg_get_constraintdef(con.oid) like '%NOT ((lote_id IS NOT NULL) AND (item_id IS NOT NULL))%') then
    raise exception 'C2S8-POSTCOND: check relaxado de vinculo da proposta (geracao S8) ausente';
  end if;
  select count(1) into n_tenants from sigov.tenant where ativo=true and is_deleted=false;
  select count(distinct tenant_id) into n_mod_tenants from sigov.compras_modalidade_config where ativo=true and entidade_id is null and esfera_governo is null;
  if n_mod_tenants < n_tenants then
    raise exception 'C2S8-POSTCOND: catalogo de modalidades sem cobertura de todos os tenants ativos (% < %)', n_mod_tenants, n_tenants;
  end if;
end
$$;
