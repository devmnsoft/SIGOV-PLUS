-- ============================================================================
-- RC-EVO Ciclo 2 Sprint 12 - RH e Previdencia: estacao de folha e previdencia
-- publica com o maximo de regras da legislacao brasileira parametrizadas no
-- banco (BR-12.1 a BR-12.8). Idempotente e nao-destrutiva ao legado (regras
-- 5/6): as geracoes A/B/C existentes de RH (rh_previdencia_parametro,
-- rh360, folha_calculo) permanecem; esta frente cria o dominio tipado de
-- calculo como autoridade da nova vertical (regra 11) sem converter nada.
-- Legislacao coberta (tudo PARAMETRIZADO no banco - regra 22/24; nenhum
-- percentual, valor ou prazo hardcoded no codigo):
--   CF/88 art. 37 XV/XVI (teto constitucional por esfera: municipal =
--     subsidio do Governador do Estado, estadual/federal = Ministro do STF,
--     valores parametricos TETO_REMUNERACAO_*), art. 40 (regime proprio);
--   EC 103/2019 (reforma das aposentadorias publicas: teto unificado);
--   LC 51/1984 (INSS progressivo por faixas com coeficiente de reducao -
--     tabela previdencia_inss_faixa por regime/ano);
--   RPPS (regime proprio: aliquota parametrica simples/progressiva - mesma
--     tabela de faixas, regime='RPPS');
--   Lei 8.112/1990 e Decreto 5.452/43 arts. 129-151 (ferias: ciclo aquisitivo
--     de 12 meses = 30 dias parametrico, aviso previo parametrico default 30
--     dias, adicional de 1/3 auditavel sobre dias gozados E abonados, abono
--     pecuniario, fracionamento em ate 3 periodos parametrico com periodo
--     minimo parametrico);
--   Lei 4.749/65 c/c Lei 12.735/2012 (13o salario: 1/12 por mes trabalhado >=
--     15 dias, 1a parcela em 30/11 e 2a em 20/12 - datas parametricas);
--   Lei 8.112/90 arts. 81-95 (afastamentos: maternidade 120 dias, paternidade
--     5 dias, adocao parametrica, doenca/em estudo; GED como documento-
--     comprovante; devolucao automatica apos o fim do prazo);
--   Decreto 3.048/99 (IRRF: tabela progressiva mensal por ano-base com
--     deducao parametrica - tabela previdencia_irrf_faixa, 2025 e 2026);
--   Portaria MTP 672/2019 (eSocial publico: eventos S-1000/S-1010/S-1005/
--     S-1006/S-1200 em lotes versionados e idempotentes por competencia).
-- Multi-esfera (regras 21-24): a esfera de governo e herdada da entidade do
-- contexto institucional na admissao (sigov.entidade.esfera_governo); teto,
-- faixas e prazos resolvem por esfera/regime/ano somente no banco.
-- Backstops (regra 13: falha nomeada, nunca sucesso simulado):
--   1 matricula funcional unica por tenant (parcial, soft-delete aware);
--   1 vinculo ATIVO por servidor (parcial);
--   1 calculo VIGENTE por vinculo x competencia (parcial; reprocesso = nova
--     versao que veta a anterior, ambas auditaveis);
--   lote eSocial unico por tenant x competencia x tipo (reprocesso reenvia o
--     MESMO lote).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1) Catalogo de naturezas de remuneracao (autoridade do banco - regra 11/12):
--    define o que incide previdencia e o que incide IRRF. Global (tenant null
--    = institucional); o engine consulta por codigo.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_natureza(
  id bigint generated always as identity primary key,
  codigo varchar(40) not null unique,
  nome varchar(120) not null,
  incide_previdencia boolean not null default false,
  incide_irrf boolean not null default false,
  ordem integer not null default 0,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);

insert into sigov.previdencia_natureza(codigo,nome,incide_previdencia,incide_irrf,ordem) values
  ('VENCIMENTO','Vencimento / Subsídio',true,true,1),
  ('ADICIONAL_TITULO','Adicional de título',true,true,2),
  ('GRATIFICACAO_FUNCAO','Gratificação de função',true,true,3),
  ('ABONO_PERMANENCIA','Abono de permanência (EC 103/2019)',true,false,4),
  ('FERIAS_GOZO','Férias (mes de gozo)',true,true,5),
  ('FERIAS_ADDICIONAL_TERCO','Adicional constitucional de 1/3 (Lei 8.112/90 art. 76; CLT 145)',false,false,6),
  ('DECIMO_SALARIO','13º salário (Lei 4.749/65)',true,true,7),
  ('OUTRAS_RENDA','Outras rendas não tributáveis (auxílios parametricos)',false,false,8),
  ('DESCONTO_OUTROS','Descontos diversos parametricos',false,false,9)
on conflict (codigo) do nothing;

-- ----------------------------------------------------------------------------
-- 2) Cadastro: cargo (classe/padrão/nível), servidor (matrícula funcional
--    única por tenant), vínculo (1 ativo por servidor) e itens de remuneração.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_cargo(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  codigo_externo varchar(60) not null,
  nome varchar(160) not null,
  classe varchar(40),
  padrao varchar(40),
  nivel varchar(40),
  modo_remuneracao varchar(24) not null default 'VENCIMENTO_ADICIONAIS' check (modo_remuneracao in ('SUBSIDIO','VENCIMENTO_ADICIONAIS')),
  valor_referencia numeric(16,2) not null default 0,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_cargo_codigo on sigov.previdencia_cargo(tenant_id,codigo_externo) where not is_deleted;
create index if not exists ix_previdencia_cargo_tenant on sigov.previdencia_cargo(tenant_id) where not is_deleted;

create table if not exists sigov.previdencia_servidor(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  matricula_funcional varchar(40) not null,
  nome varchar(160) not null,
  esfera_governo varchar(12) not null check (esfera_governo in ('municipal','estadual','federal')),
  uf char(2),
  data_cadastro date not null default current_date,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
-- backstop: 1 matricula funcional unica por tenant (BR-12.1)
create unique index if not exists ux_previdencia_servidor_matricula on sigov.previdencia_servidor(tenant_id,matricula_funcional) where not is_deleted;
create index if not exists ix_previdencia_servidor_tenant on sigov.previdencia_servidor(tenant_id) where not is_deleted;

create table if not exists sigov.previdencia_vinculo(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  servidor_id bigint not null references sigov.previdencia_servidor(id),
  cargo_id bigint not null references sigov.previdencia_cargo(id),
  tipo_vinculo varchar(20) not null default 'EFETIVO' check (tipo_vinculo in ('EFETIVO','COMISSAO','TEMPORARIO')),
  data_admissao date not null,
  situacao varchar(16) not null default 'ATIVO' check (situacao in ('ATIVO','DESLIGADO','APOSENTADO')),
  data_desligamento date,
  motivo_desligamento text,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
-- backstop: no maximo 1 vinculo ATIVO por servidor (BR-12.1)
create unique index if not exists ux_previdencia_vinculo_ativo on sigov.previdencia_vinculo(tenant_id,servidor_id) where situacao='ATIVO' and not is_deleted;
create index if not exists ix_previdencia_vinculo_cargo on sigov.previdencia_vinculo(cargo_id) where not is_deleted;

create table if not exists sigov.previdencia_remun_item(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  vinculo_id bigint not null references sigov.previdencia_vinculo(id),
  natureza_codigo varchar(40) not null,
  valor numeric(16,2) not null check (valor >= 0),
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_remun_natureza on sigov.previdencia_remun_item(vinculo_id,natureza_codigo) where not is_deleted;
create index if not exists ix_previdencia_remun_vinculo on sigov.previdencia_remun_item(vinculo_id) where not is_deleted;

-- ----------------------------------------------------------------------------
-- 3) Folha: competencia unica por tenant x mes, calculo versionado por
--    vinculo x competencia (backstop 1 VIGENTE) e demonstracao item a item.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_folha_competencia(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  competencia date not null check (extract(day from competencia)=1),
  status varchar(12) not null default 'ABERTA' check (status in ('ABERTA','FECHADA')),
  fechada_em timestamptz,
  usuario_fechou bigint,
  reaberta_em timestamptz,
  justificativa_reabertura text,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_folha_competencia on sigov.previdencia_folha_competencia(tenant_id,competencia);

create table if not exists sigov.previdencia_folha_calculo(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  competencia_id bigint not null references sigov.previdencia_folha_competencia(id),
  vinculo_id bigint not null references sigov.previdencia_vinculo(id),
  versao integer not null default 1,
  data_base_calculo date not null,
  bruto numeric(16,2) not null default 0,
  base_previdencia numeric(16,2) not null default 0,
  inss_valor numeric(16,2) not null default 0,
  base_irrf numeric(16,2) not null default 0,
  irrf_valor numeric(16,2) not null default 0,
  decimo_valor numeric(16,2) not null default 0,
  liquido numeric(16,2) not null default 0,
  status varchar(10) not null default 'VIGENTE' check (status in ('VIGENTE','VETADA')),
  motivo_veto text,
  vetada_em timestamptz,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
-- backstop BR-12.2: exatamente 1 calculo VIGENTE por vinculo x competencia
create unique index if not exists ux_previdencia_calculo_vigente on sigov.previdencia_folha_calculo(tenant_id,vinculo_id,competencia_id) where status='VIGENTE';
create index if not exists ix_previdencia_calculo_competencia on sigov.previdencia_folha_calculo(competencia_id);

create table if not exists sigov.previdencia_folha_calculo_item(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  calculo_id bigint not null references sigov.previdencia_folha_calculo(id),
  sequencia integer not null default 0,
  grupo varchar(20) not null check (grupo in ('RENDA','PREVIDENCIA','IRRF','DECIMO_SALARIO')),
  descricao varchar(200) not null,
  natureza_codigo varchar(40),
  base numeric(16,2),
  aliquota_pct numeric(7,4),
  valor numeric(16,2) not null default 0,
  formula varchar(500),
  created_at timestamptz not null default now(),
  created_by bigint
);
create index if not exists ix_previdencia_calc_item on sigov.previdencia_folha_calculo_item(calculo_id);

-- ----------------------------------------------------------------------------
-- 4) Ferias (Lei 8.112/90; CLT arts. 134-149): ciclo aquisitivo, programacao
--    com aviso previo, adicional de 1/3 auditavel e fracionamento parametrico.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_ferias(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  vinculo_id bigint not null references sigov.previdencia_vinculo(id),
  inicio_aquisitivo date not null,
  fim_aquisitivo date not null,
  dias_adquiridos integer not null default 0,
  dias_gozados integer not null default 0,
  dias_abonados integer not null default 0,
  saldo_dias integer not null default 0,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_ferias_ciclo on sigov.previdencia_ferias(tenant_id,vinculo_id,inicio_aquisitivo);

create table if not exists sigov.previdencia_ferias_programacao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  ferias_id bigint not null references sigov.previdencia_ferias(id),
  data_inicio date not null,
  data_fim date not null,
  dias_goze integer not null check (dias_goze > 0),
  dias_abono integer not null default 0 check (dias_abono >= 0),
  salario_dia numeric(16,2) not null default 0,
  adicional_terco numeric(16,2) not null default 0,
  status varchar(12) not null default 'PROGRAMADA' check (status in ('PROGRAMADA','GOZO','CANCELADA')),
  gozo_em timestamptz,
  justificativa text,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create index if not exists ix_previdencia_ferias_prog on sigov.previdencia_ferias_programacao(ferias_id);

-- ----------------------------------------------------------------------------
-- 5) Afastamentos (Lei 8.112/90 arts. 81-95): duracao por tipo parametrizada,
--    GED como documento-comprovante, devolucao apos o fim do prazo.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_afastamento(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  vinculo_id bigint not null references sigov.previdencia_vinculo(id),
  tipo varchar(16) not null check (tipo in ('SAUDE','ESTUDO','MATERNIDADE','PATERNIDADE','ADOCAO')),
  regime_opcao varchar(8) not null default 'RPSS' check (regime_opcao in ('RGPS','RPSS')),
  data_inicio date not null,
  data_fim date not null,
  documento_ged varchar(120) not null,
  status varchar(10) not null default 'ATIVO' check (status in ('ATIVO','DEVOLVIDO')),
  devolvido_em timestamptz,
  motivo_devolucao text,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create index if not exists ix_previdencia_afast_vinculo on sigov.previdencia_afastamento(vinculo_id);

-- ----------------------------------------------------------------------------
-- 6) Decimo salario (Lei 4.749/65 c/c Lei 12.735/2012): acumulacao anual por
--    vinculo x ano, 1a parcela em novembro e 2a em dezembro (datas
--    parametricas), 1/12 por mes com pelo menos 15 dias trabalhados.
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_decimo(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  vinculo_id bigint not null references sigov.previdencia_vinculo(id),
  ano integer not null,
  meses_ate_1a_parcela integer,
  meses_finais integer,
  parcela1_valor numeric(16,2),
  parcela2_valor numeric(16,2),
  parcela1_calculo_id bigint,
  parcela2_calculo_id bigint,
  status varchar(20) not null default 'EM_ACUMULO' check (status in ('EM_ACUMULO','PARCELA1_REGISTRADA','ENCERRADO')),
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_decimo_ano on sigov.previdencia_decimo(tenant_id,vinculo_id,ano);

-- ----------------------------------------------------------------------------
-- 7) eSocial publico (Portaria MTP 672/2019): lote unico por tenant x
--    competencia x tipo (reprocesso idempotente reenvia o MESMO lote) e
--    eventos com validacao previa ao envio (erro bloqueia, nao simula).
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_esocial_lote(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  competencia date not null,
  lote_tipo varchar(24) not null check (lote_tipo in ('TABELAS_S1000_S1010','ADMISAO_S1005','MOVIMENTACAO_S1006','REMUNERACAO_S1200')),
  versao integer not null default 1,
  status varchar(24) not null default 'GERADO' check (status in ('GERADO','VALIDADO','BLOQUEADO_VALIDACAO')),
  total_eventos integer not null default 0,
  eventos_com_erro integer not null default 0,
  gerado_em timestamptz not null default now(),
  enviado_em timestamptz,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);
create unique index if not exists ux_previdencia_esocial_lote on sigov.previdencia_esocial_lote(tenant_id,competencia,lote_tipo);

create table if not exists sigov.previdencia_esocial_evento(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  lote_id bigint not null references sigov.previdencia_esocial_lote(id),
  sequencia integer not null,
  evento varchar(16) not null,
  alvo_texto varchar(120) not null default '',
  payload jsonb not null default '{}'::jsonb,
  validacao_ok boolean not null default true,
  mensagens text[] not null default '{}',
  created_at timestamptz not null default now(),
  created_by bigint
);
create index if not exists ix_previdencia_esocial_evento on sigov.previdencia_esocial_evento(lote_id);

-- ----------------------------------------------------------------------------
-- 8) Tabelas parametricas de faixas (banco = autoridade - regra 11):
--    INSS/RPPS por regime + ano (LC 51/1984: faixas progressivas com
--    coeficiente de reducao na faixa de teto; RPPS: faixa unica plana ou
--    progressiva conforme a lei estadual) e IRRF por ano-base (Decreto
--    3.048/99: alíquota + dedução).
-- ----------------------------------------------------------------------------
create table if not exists sigov.previdencia_inss_faixa(
  id bigint generated always as identity primary key,
  regime varchar(20) not null check (regime in ('RGPS','RPPS')),
  ano integer not null,
  ordem integer not null,
  limite_min numeric(16,2) not null default 0,
  limite_max numeric(16,2),
  aliquota_pct numeric(7,4) not null,
  coeficiente_reducao numeric(9,6) not null default 0,
  created_at timestamptz not null default now(),
  created_by bigint
);
create unique index if not exists ux_previdencia_inss_faixa on sigov.previdencia_inss_faixa(regime,ano,ordem);

insert into sigov.previdencia_inss_faixa(regime,ano,ordem,limite_min,limite_max,aliquota_pct,coeficiente_reducao) values
  -- RGPS (LC 51/1984, estrutura progressiva; valores de homologacao 2025/2026)
  ('RGPS',2025,1,0.00,1518.00,7.5,0),
  ('RGPS',2025,2,1518.00,2793.88,9.0,0),
  ('RGPS',2025,3,2793.88,4190.83,12.0,0),
  ('RGPS',2025,4,4190.83,8157.41,14.0,0.1151),
  ('RGPS',2026,1,0.00,1518.00,7.5,0),
  ('RGPS',2026,2,1518.00,2793.88,9.0,0),
  ('RGPS',2026,3,2793.88,4190.83,12.0,0),
  ('RGPS',2026,4,4190.83,8157.41,14.0,0.1151),
  -- RPPS (regime proprio: aliquota parametrica de exemplo - a lei de cada
  -- esfera define a tabela real; faixa unica plana cobre qualquer base)
  ('RPPS',2025,1,0.00,NULL,11.0,0),
  ('RPPS',2026,1,0.00,NULL,11.0,0)
on conflict (regime,ano,ordem) do nothing;

create table if not exists sigov.previdencia_irrf_faixa(
  id bigint generated always as identity primary key,
  ano_base integer not null,
  ordem integer not null,
  limite_min numeric(16,2) not null default 0,
  limite_max numeric(16,2),
  aliquota_pct numeric(7,4) not null default 0,
  deducao numeric(16,2) not null default 0,
  created_at timestamptz not null default now(),
  created_by bigint
);
create unique index if not exists ux_previdencia_irrf_faixa on sigov.previdencia_irrf_faixa(ano_base,ordem);

insert into sigov.previdencia_irrf_faixa(ano_base,ordem,limite_min,limite_max,aliquota_pct,deducao) values
  -- Tabela progressiva mensal IRRF 2025 (Decreto 3.048/99; dedução oficial)
  (2025,1,0.00,2259.20,0.0,0.00),
  (2025,2,2259.20,2826.65,7.5,169.44),
  (2025,3,2826.65,3751.05,15.0,381.44),
  (2025,4,3751.05,4664.68,22.5,662.77),
  (2025,5,4664.68,NULL,27.5,896.00),
  -- Tabela progressiva mensal IRRF 2026 (faixa isenta ampliada a R$ 3.036,00)
  (2026,1,0.00,3036.00,0.0,0.00),
  (2026,2,3036.00,4705.83,7.5,227.56),
  (2026,3,4705.83,6891.28,15.0,591.90),
  (2026,4,6891.28,9798.62,22.5,1107.35),
  (2026,5,9798.62,NULL,27.5,1600.16)
on conflict (ano_base,ordem) do nothing;

-- ----------------------------------------------------------------------------
-- 9) Parametros da frente (catalogo sigov.parametro_modulo + override por
--    tenant em sigov.parametro_modulo_valor - regra 11/22): todos os prazos,
--    limites e datas abaixo sao resolvidos somente no banco.
-- ----------------------------------------------------------------------------
insert into sigov.parametro_modulo(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo,is_deleted)
select v.modulo,v.codigo,v.nome,v.descricao,v.tipo,(v.valor_padrao)::jsonb,v.sensivel,v.ordem,v.ativo,v.is_deleted from (values
  ('PREVIDENCIA','PREVIDENCIA_REGIME','Regime de previdencia do calculo',
   'RGPS = faixas progressivas LC 51/1984; RPPS = tabela do regime proprio (parametrica). Resolvido por tenant com override.',
   'TEXT','"RGPS"',false,1,true,false),
  ('PREVIDENCIA','TETO_REMUNERACAO_MUNICIPAL','Teto constitucional - esferas municipais',
   'CF/88 art. 37 XV: subsidio do Prefeito limitado ao subsidio do Governador do Estado (valor definido pela lei estadual). Zero = livre por design (deve ser configurado quando houver teto estadual).',
   'DECIMAL','0.00',false,2,true,false),
  ('PREVIDENCIA','TETO_REMUNERACAO_ESTADUAL','Teto constitucional - esferas estaduais',
   'EC 103/2019: limite correspondente a remuneracao do Ministro do STF (valor de homologacao; atualizar por ato).',
   'DECIMAL','44161.63',false,3,true,false),
  ('PREVIDENCIA','TETO_REMUNERACAO_FEDERAL','Teto constitucional - esferas federais',
   'EC 103/2019: limite correspondente a remuneracao do Ministro do STF (valor de homologacao; atualizar por ato).',
   'DECIMAL','44161.63',false,4,true,false),
  ('PREVIDENCIA','FERIAS_DIAS_POR_CICLO','Dias de ferias por ciclo aquisitivo',
   'Ciclo de 12 meses de servico gera o periodo gozo (CLT art. 130; Lei 8.112/90 art. 82 - 30 dias).',
   'DECIMAL','30',false,5,true,false),
  ('PREVIDENCIA','FERIAS_INTERVALO_MINIMO_DIAS','Aviso previo minimo para ferias',
   'Intervalo minimo entre a data do calculo e o inicio do gozo (Lei 8.112/90 art. 84: aviso de 30 dias).',
   'DECIMAL','30',false,6,true,false),
  ('PREVIDENCIA','FERIAS_FRACIONAMENTO_MAXIMO','Fracionamento maximo de periodos',
   'Periodos maximos de gozo/abono por ciclo aquisitivo (Lei 8.112/90: ate 3; CLT art. 143).',
   'DECIMAL','3',false,7,true,false),
  ('PREVIDENCIA','FERIAS_FRAC_MINIMO_DIAS','Periodo minimo de fracionamento',
   'Dias minimos de gozo por periodo quando fracionado (CLT art. 143: 5 dias).',
   'DECIMAL','5',false,8,true,false),
  ('PREVIDENCIA','FERIAS_ACONTO_MINIMO_DIAS','Abono pecuniario minimo',
   'Dias minimos de abono pecuniario por vez (CLT art. 145: 5 dias).',
   'DECIMAL','5',false,9,true,false),
  ('PREVIDENCIA','DECIMO_1A_PARCELA_MES','Mes da 1a parcela do 13o salario',
   'Lei 4.749/65 art. 7: 1a parcela ate 30 de novembro (mes parametrizado).',
   'DECIMAL','11',false,10,true,false),
  ('PREVIDENCIA','DECIMO_1A_PARCELA_DIA','Dia da 1a parcela do 13o salario','Dia-limite da 1a parcela (default 30/11).','DECIMAL','30',false,11,true,false),
  ('PREVIDENCIA','DECIMO_2A_PARCELA_MES','Mes da 2a parcela do 13o salario','Lei 4.749/65 art. 8: 2a parcela ate 20 de dezembro (mes parametrizado).','DECIMAL','12',false,12,true,false),
  ('PREVIDENCIA','DECIMO_2A_PARCELA_DIA','Dia da 2a parcela do 13o salario','Dia-limite da 2a parcela (default 20/12).','DECIMAL','20',false,13,true,false),
  ('PREVIDENCIA','AFASTAMENTO_MATERNIDADE_DIAS','Licenca-maternidade (dias)',
   'CF/88 art. 7 XIX: 120 dias ininterruptos (Lei 8.112/90 art. 81).',
   'DECIMAL','120',false,14,true,false),
  ('PREVIDENCIA','AFASTAMENTO_PATERNIDADE_DIAS','Licenca-paternidade (dias)',
   'CF/88 art. 7 XIX (redacao EC 120/2022) e Lei 8.112/90 art. 81: 5 dias.',
   'DECIMAL','5',false,15,true,false),
  ('PREVIDENCIA','AFASTAMENTO_ADOCAO_DIAS','Licenca por adocao (dias)',
   'CF/88 art. 7 XIX: 120/60/30 dias conforme idade da crianca (teto parametrizado aqui para homologacao).',
   'DECIMAL','120',false,16,true,false),
  ('PREVIDENCIA','CONTRACHEQUE_RETENCAO_MESES','Retencao do contracheque versionado (meses)',
   'LGPD art. 5: minimizacao e limitacao da finalidade - periodo de retencao dos contracheques auditaveis.',
   'DECIMAL','24',false,17,true,false)
) v(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo,is_deleted)
where not exists(select 1 from sigov.parametro_modulo p where p.modulo=v.modulo and p.codigo=v.codigo and not p.is_deleted);

-- ----------------------------------------------------------------------------
-- 10) Catalogo de permissoes da frente (chave canonica por ato - regra 11/12)
--     + vinculo do perfil sistemico SUPERADMIN (padrao S7 documentado).
-- ----------------------------------------------------------------------------
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'previdencia',v.chave,v.recurso,v.acao,v.descricao,true,false from (values
  ('previdencia.visualizar','previdencia','visualizar','Visualizar cadastros, folha, competencias, parametros e dashboards da estacao de previdencia - BR-12.x'),
  ('previdencia.cargo.criar','previdencia.cargo','criar','Criar/editar cargos (classe/padrao/nivel, modo subsidio ou vencimento+adicionais) e remuneracao do vinculo; desligar servidor - BR-12.1'),
  ('previdencia.servidor.criar','previdencia.servidor','criar','Admitir servidor (matricula unica por tenant, 1 vinculo ativo, esfera herdada da entidade) - BR-12.1'),
  ('previdencia.folha.fechar','previdencia.folha','fechar','Abrir/reabrir e fechar competencia com calculo deterministico (INSS/RPPS por faixa, IRRF progressivo por ano, 13o, teto constitucional por esfera) - BR-12.2/12.5/12.6'),
  ('previdencia.folha.reprocessar','previdencia.folha','reprocessar','Reprocessar calculo individual: nova versao que veta a anterior (ambas auditaveis) - BR-12.3'),
  ('previdencia.ferias.apurar','previdencia.ferias','apurar','Apurar aquisicao de dias por ciclo aquisitivo concluido (12 meses) - BR-12.4'),
  ('previdencia.ferias.programar','previdencia.ferias','programar','Programar gozo/abono (aviso previo, fracionamento, adicional 1/3 auditavel, saldo) - BR-12.4'),
  ('previdencia.afastamento.registrar','previdencia.afastamento','registrar','Registrar/devolver afastamentos (doenca RGPS/RPSS, estudo, maternidade 120d, paternidade 5d, adocao; GED obrigatorio) - BR-12.4'),
  ('previdencia.contracheque.visualizar','previdencia.contracheque','visualizar','Visualizar contracheque versionado com demonstracao (LGPD: dados minimos) - BR-12.7'),
  ('previdencia.esocial.enviar','previdencia.esocial','enviar','Gerar/enviar lote eSocial publico por competencia (S-1000/S-1010/S-1005/S-1006/S-1200) com validacao previa - BR-12.8')
) v(chave,recurso,acao,descricao) where not exists(select 1 from sigov.permissao p where p.chave=v.chave);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false
  from sigov.perfil_acesso pa
  cross join sigov.permissao p
 where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
   and p.modulo='previdencia' and p.ativo and not p.is_deleted
   and p.chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar',
    'previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar',
    'previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar',
    'previdencia.esocial.enviar')
on conflict (perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;

-- C2S12-POSTCOND: falha explicita se faltar qualquer artefato (regra 13)
do $$
declare
  n_chaves int;
  n_perfis int;
  n_vinculos int;
  n_naturezas int;
  n_inss int;
  n_irrf int;
  n_parametros int;
begin
  select count(1) into n_chaves
    from sigov.permissao
   where modulo='previdencia' and ativo and not is_deleted
     and chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar',
      'previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar',
      'previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar',
      'previdencia.esocial.enviar');
  if n_chaves<>10 then
    raise exception 'C2S12-POSTCOND: catalogo de permissoes incompleto (% <> 10)', n_chaves;
  end if;
  select count(1) into n_perfis from sigov.perfil_acesso where codigo_externo='SUPERADMIN' and ativo and not is_deleted;
  if n_perfis=0 then
    raise exception 'C2S12-POSTCOND: nenhum perfil SUPERADMIN ativo no banco (autoridade ausente)';
  end if;
  select count(1) into n_vinculos
    from sigov.perfil_permissao pp
    join sigov.permissao p on p.id=pp.permissao_id
    join sigov.perfil_acesso pa on pa.id=pp.perfil_acesso_id
   where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
     and pp.ativo and not pp.is_deleted
     and p.modulo='previdencia'
     and p.chave in ('previdencia.visualizar','previdencia.cargo.criar','previdencia.servidor.criar',
      'previdencia.folha.fechar','previdencia.folha.reprocessar','previdencia.ferias.apurar',
      'previdencia.ferias.programar','previdencia.afastamento.registrar','previdencia.contracheque.visualizar',
      'previdencia.esocial.enviar');
  if n_vinculos<>n_perfis*10 then
    raise exception 'C2S12-POSTCOND: vinculos SUPERADMIN incompletos (% <> %*10)', n_vinculos, n_perfis;
  end if;
  select count(1) into n_naturezas from sigov.previdencia_natureza where ativo and not is_deleted;
  if n_naturezas<9 then
    raise exception 'C2S12-POSTCOND: catalogo de naturezas incompleto (% < 9)', n_naturezas;
  end if;
  select count(1) into n_inss from sigov.previdencia_inss_faixa where ano=2026;
  if n_inss<>5 then
    raise exception 'C2S12-POSTCOND: faixas de previdencia 2026 incompletas (% <> 5: RGPS 4 + RPPS 1)', n_inss;
  end if;
  select count(1) into n_irrf from sigov.previdencia_irrf_faixa where ano_base=2026;
  if n_irrf<>5 then
    raise exception 'C2S12-POSTCOND: faixas de IRRF 2026 incompletas (% <> 5)', n_irrf;
  end if;
  select count(1) into n_parametros from sigov.parametro_modulo where modulo='PREVIDENCIA' and ativo and not is_deleted;
  if n_parametros<17 then
    raise exception 'C2S12-POSTCOND: parametros PREVIDENCIA incompletos (% < 17)', n_parametros;
  end if;
end
$$;
