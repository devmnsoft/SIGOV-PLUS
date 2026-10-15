-- ============================================================================
-- RC-EVO Ciclo 2 Sprint 13 - Orcamento e Responsabilidade Fiscal (LRF)
-- multi-esfera: dominio tipado de orcamento publico com a estrutura da Lei
-- 4.320/64 e os limites/prestacoes de contas da LC 101/2000 (BR-13.1 a
-- BR-13.8). Idempotente e nao-destrutiva ao legado (regras 5/6): as geracoes
-- existentes (sigov.orcamento_* do SIAFIC e financeiro_*/contabilidade_* da
-- RC50.86) permanecem; esta frente cria o dominio tipado orcamento_* como
-- autoridade da nova vertical (regra 11) sem converter nada.
-- Legislacao coberta (tudo PARAMETRIZADO no banco - regra 22/24):
--   Lei 4.320/64 (estrutura da classificacao: orgao > unidade > funcao >
--     subfuncao > programa > acao (projeto/atividade/subprojeto); categorias
--     e grupos economicos; disponibilidade = dotacao inicial + creditos
--     adicionais, sem rollover entre exercicios);
--   LC 101/2000 art. 20 (limites de despesa com pessoal: executivo 50% da
--     RCL; art. 19/20 legislatura 25% do executivo e judiciario 60% da
--     legislatura), art. 32 e arts. correlatos (restos a pagar monitorados),
--   art. 51 (previsao bimestral automaticamente atualizada e publicada) e
--   art. 54 (RREO semestral/anual com conjuntos minimos);
--   CF/88 art. 212 (minimos constitucionais de saude e educacao - MDE 25%
--     por esfera municipal/estadual; federal sem minimo constitucional =
--     valor zero, parametrizavel por lei complementar/organica);
--   Leis estaduais/organas: todo limite e percentual entra como linha do
--     catalogo orcamento_limite_lrf por esfera com override por tenant
--     (nenhum percentual hardcoded no codigo).
-- Multi-esfera (regras 21-24): a esfera de governo e herdada da entidade do
-- contexto institucional (sigov.entidade.esfera_governo); limites, bases e
-- percentuais resolvem por esfera somente no banco; NULL de esfera falha
-- explicita (ESFERA_NAO_CONFIGURADA, regra 13).
-- Backstops (regra 13: falha nomeada, nunca sucesso simulado):
--   1 dotacao por tenant x entidade x exercicio x acao x fonte (parcial);
--   1 chave de idempotencia por tenant (credito/empenho/liquidacao);
--   1 previsao bimestral VIGENTE por tenant x exercicio x bimestre (parcial);
--   1 RREO VIGENTE por tenant x exercicio x tipo x semestre (parcial);
--   1 estado por exercicio (parcial);
--   funcao/subfuncao como catalogos globais com codigo unico;
--   guard de schema com falha nomeada ORCAMENTO_NAO_DISPONIVEL (404).
-- Regras centrais do motor (transacionais, 1 ato = 1 transacao):
--   disponibilidade(dota) = inicial + suplementares + especiais +
--     extraordinarios - reducoes - empenhado;
--   empenho (CP) so abre com disponibilidade suficiente em TODOS os itens
--     (EMPENHO_ORCAMENTO_INSUFICIENTE, 409, rollback integral);
--   liquidacao <= empenho disponivel em cada item
--     (LIQUIDACAO_SUPER_EMPENHO, 409);
--   exercicio ENCERRADO bloqueia empenho/liquidacao/credito novo
--     (EXERCICIO_ENCERRADO, 409); reabertura exige justificativa;
--   apuracao LRF apos cada movimento (parametrica): percentual calculado
--     sobre base de receita parametrizada; DENTRO/ALERTA/BLOQUEIO; quando
--     BLOQUEIO com o limite aplicavel ao empenho, o empenho novo e negado
--     (LRF_LIMITE_BLOQUEADO, 409) - o bloqueio nasce dos dados reais, nunca
--     de catalogo ficticio (regra 12).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1) Classificacao funcional nacional (Lei 4.320/64 - catalogos globais):
--    funcao -> subfuncao. Flags de area para a apuracao LRF (MDE = saude +
--    educacao; funcao social para o minimo parametrico). Global: sem
--    tenant_id (institucional), referencia por codigo (padrao S12).
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_funcao(
  id bigint generated always as identity primary key,
  codigo varchar(40) not null unique,
  nome varchar(160) not null,
  funcao_social boolean not null default false,
  area_saude_educacao boolean not null default false,
  ordem integer not null default 0,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);

insert into sigov.orcamento_funcao(codigo,nome,funcao_social,area_saude_educacao,ordem) values
  ('01','LEGISLATIVA',false,false,1),
  ('02','JUDICIARIA',false,false,2),
  ('03','DEFESA CIVIL',false,false,3),
  ('04','ASSISTENCIA E ORDENACAO GERAL',false,false,4),
  ('05','SEGURIDADE PUBLICA',true,false,5),
  ('06','TRANSPORTE',true,false,6),
  ('07','TECNOLOGIA DA INFORMACAO E COMUNICACAO',true,false,7),
  ('08','TRABALHO E EMPREGO',true,false,8),
  ('09','SAUDE',true,true,9),
  ('10','EDUCACAO',true,true,10),
  ('11','CULTURA E ESPORTE',true,false,11),
  ('12','TURISMO',true,false,12),
  ('13','MEIO AMBIENTE',true,false,13),
  ('14','DESENVOLVIMENTO URBANO',true,false,14),
  ('15','DESENVOLVIMENTO AGRICOLO',true,false,15),
  ('16','DESENVOLVIMENTO ECONOMICO',true,false,16),
  ('17','PREVIDENCIA',true,false,17),
  ('18','ASSISTENCIA SOCIAL',true,false,18),
  ('19','VIVENCIA',true,false,19)
on conflict (codigo) do nothing;

create table if not exists sigov.orcamento_subfuncao(
  id bigint generated always as identity primary key,
  codigo varchar(40) not null unique,
  nome varchar(160) not null,
  funcao_codigo varchar(40) not null,
  ordem integer not null default 0,
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  constraint ck_orcamento_subfuncao_funcao foreign key (funcao_codigo) references sigov.orcamento_funcao(codigo)
);

insert into sigov.orcamento_subfuncao(codigo,nome,funcao_codigo,ordem) values
  ('091','PREVENC E ASSIST MED HOSP POPULACIONAL','09',1),
  ('301','VIGILANCIA SANITARIA','09',2),
  ('302','ASSISTENCIA HOSPITALAR E AMBULATORIAL','09',3),
  ('122','EDUCACAO BASICA: ENSINO FUNDAMENTAL','10',4),
  ('128','FORMACAO DE RECURSOS HUMANOS NA SAUDE','10',5),
  ('271','PREVINCIA, ASSISTENCIA E PLANEJAMENTO PREVIDENCIARIO','17',6),
  ('264','DESENVOLVIMENTO URBANO DO MUNICIPIO','14',7),
  ('285','GESTAO DA POLITICA DE ASSISTENCIA SOCIAL','18',8),
  ('365','POLITICA DE SEGURANCA ALIMENTAR E NUTRICIONAL','18',9)
on conflict (codigo) do nothing;

-- ----------------------------------------------------------------------------
-- 2) Estrutura orçamentária por exercício (BR-13.1): programa e ação
--    (projeto/atividade/subprojeto) com categoria e grupo econômicos
--    (Lei 4.320/64 art. 12). Esfera herdada da entidade do contexto.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_programa(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  codigo varchar(30) not null,
  nome varchar(200) not null,
  objetivo text not null,
  situacao varchar(20) not null default 'ATIVO' check (situacao in ('ATIVO','ENCERRADO')),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_programa_codigo
  on sigov.orcamento_programa(tenant_id,exercicio_id,codigo) where not is_deleted;
create index if not exists ix_orcamento_programa_ctx
  on sigov.orcamento_programa(tenant_id,entidade_id,exercicio_id) where not is_deleted;

create table if not exists sigov.orcamento_acao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  programa_id bigint null references sigov.orcamento_programa(id),
  codigo varchar(30) not null,
  nome varchar(200) not null,
  tipo varchar(20) not null check (tipo in ('PROJETO','ATIVIDADE','SUBPROJETO')),
  subfuncao_codigo varchar(40) not null,
  funcao_codigo varchar(40) not null,
  poder varchar(10) not null check (poder in ('EXECUTIVO','LEGISLATIVO','JUDICIARIO')),
  categoria_economica varchar(30) not null
    check (categoria_economica in ('CORRENTE','INVESTIMENTO','AMORTIZACAO_DIVIDA','OUTRA_DESPESA')),
  grupo_despesa varchar(40) not null
    check (grupo_despesa in ('PESSOAL_E_ENCARGOS','JUROS_E_ENCARGOS_DIVIDA','OUTRAS_DESPESAS_CORRENTES','INVESTIMENTOS','AMORTIZACAO_DA_DIVIDA','OUTRAS_DESPESAS_CAPITAIS')),
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz,
  constraint ck_orcamento_acao_subfuncao foreign key (subfuncao_codigo) references sigov.orcamento_subfuncao(codigo),
  constraint ck_orcamento_acao_funcao foreign key (funcao_codigo) references sigov.orcamento_funcao(codigo)
);

create unique index if not exists ux_orcamento_acao_codigo
  on sigov.orcamento_acao(tenant_id,exercicio_id,codigo) where not is_deleted;
create index if not exists ix_orcamento_acao_ctx
  on sigov.orcamento_acao(tenant_id,entidade_id,exercicio_id) where not is_deleted;
create index if not exists ix_orcamento_acao_programa
  on sigov.orcamento_acao(programa_id) where not is_deleted;

-- ----------------------------------------------------------------------------
-- 3) Dotação e créditos adicionais (BR-13.2): a linha orçamentária é
--    única por tenant x entidade x exercício x ação x fonte. Disponibilidade
--    = inicial + creditos adicionais - reducoes - empenhado. SEM ROLLOVER:
--    cada exercício tem suas próprias dotações; o saldo não transferido vira
--    indicador de restos/apuração, nunca dotação do exercício seguinte.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_dotacao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  acao_id bigint not null references sigov.orcamento_acao(id),
  fonte_recurso_codigo varchar(30) not null,
  dotacao_inicial numeric(18,2) not null check (dotacao_inicial >= 0),
  creditos_suplementares numeric(18,2) not null default 0 check (creditos_suplementares >= 0),
  creditos_especiais numeric(18,2) not null default 0 check (creditos_especiais >= 0),
  creditos_extraordinarios numeric(18,2) not null default 0 check (creditos_extraordinarios >= 0),
  reducoes numeric(18,2) not null default 0 check (reducoes >= 0),
  empenhado numeric(18,2) not null default 0 check (empenhado >= 0),
  liquidado numeric(18,2) not null default 0 check (liquidado >= 0),
  pago numeric(18,2) not null default 0 check (pago >= 0),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_dotacao_linha
  on sigov.orcamento_dotacao(tenant_id,entidade_id,exercicio_id,acao_id,fonte_recurso_codigo) where not is_deleted;
create index if not exists ix_orcamento_dotacao_ctx
  on sigov.orcamento_dotacao(tenant_id,exercicio_id) where not is_deleted;

create table if not exists sigov.orcamento_credito_adicional(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  dotacao_id bigint null references sigov.orcamento_dotacao(id),
  tipo varchar(20) not null check (tipo in ('SUPLEMENTAR','ESPECIAL','EXTRAORDINARIO')),
  valor numeric(18,2) not null check (valor > 0),
  ato_legal varchar(120) not null,
  justificativa text not null,
  chave_idempotencia varchar(80) not null,
  aprovado_por bigint,
  situacao varchar(20) not null default 'APLICADO' check (situacao in ('APLICADO','ANULADO')),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_credito_chave
  on sigov.orcamento_credito_adicional(tenant_id,chave_idempotencia) where not is_deleted;
create index if not exists ix_orcamento_credito_ctx
  on sigov.orcamento_credito_adicional(tenant_id,exercicio_id) where not is_deleted;
create index if not exists ix_orcamento_credito_dotacao
  on sigov.orcamento_credito_adicional(dotacao_id) where not is_deleted;

-- ----------------------------------------------------------------------------
-- 4) Empenho CP (BR-13.3) e liquidação SL (BR-13.4): cabecalho + itens por
--    linha de dotação. Chave de idempotência por tenant. O motor valida a
--    disponibilidade (empenho) e o empenho disponível (liquidação) em
--    transação única com lock FOR UPDATE sobre dotação/empenho/item.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_empenho(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  numero varchar(40) not null,
  data_empenho date not null,
  fornecedor_pessoa_id bigint null references sigov.pessoa(id),
  tipo_empenho varchar(20) not null default 'CP' check (tipo_empenho in ('CP','CP_GLOBAL','SUPLEMENTAR')),
  historico text not null,
  valor_total numeric(18,2) not null check (valor_total > 0),
  valor_anulado numeric(18,2) not null default 0 check (valor_anulado >= 0),
  valor_liquidado numeric(18,2) not null default 0 check (valor_liquidado >= 0),
  chave_idempotencia varchar(80) not null,
  anulado_em timestamptz null,
  anulado_por bigint,
  anulacao_justificativa text null,
  situacao varchar(20) not null default 'ATIVO' check (situacao in ('ATIVO','ANULADO')),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_empenho_chave
  on sigov.orcamento_empenho(tenant_id,chave_idempotencia) where not is_deleted;
create index if not exists ix_orcamento_empenho_ctx
  on sigov.orcamento_empenho(tenant_id,exercicio_id) where not is_deleted;
create index if not exists ix_orcamento_empenho_fornecedor
  on sigov.orcamento_empenho(fornecedor_pessoa_id) where not is_deleted;

create table if not exists sigov.orcamento_empenho_item(
  id bigint generated always as identity primary key,
  empenho_id bigint not null references sigov.orcamento_empenho(id),
  dotacao_id bigint not null references sigov.orcamento_dotacao(id),
  valor numeric(18,2) not null check (valor > 0),
  valor_anulado numeric(18,2) not null default 0 check (valor_anulado >= 0),
  valor_liquidado numeric(18,2) not null default 0 check (valor_liquidado >= 0),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now()
);

create index if not exists ix_orcamento_empenho_item_emp
  on sigov.orcamento_empenho_item(empenho_id) where not is_deleted;
create index if not exists ix_orcamento_empenho_item_dota
  on sigov.orcamento_empenho_item(dotacao_id) where not is_deleted;

create table if not exists sigov.orcamento_liquidacao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  empenho_id bigint not null references sigov.orcamento_empenho(id),
  numero varchar(40) not null,
  data_liquidacao date not null,
  historico text not null,
  valor_total numeric(18,2) not null check (valor_total > 0),
  valor_anulado numeric(18,2) not null default 0 check (valor_anulado >= 0),
  chave_idempotencia varchar(80) not null,
  anulada_em timestamptz null,
  anulada_por bigint,
  anulacao_justificativa text null,
  situacao varchar(20) not null default 'ATIVA' check (situacao in ('ATIVA','ANULADA')),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_liquidacao_chave
  on sigov.orcamento_liquidacao(tenant_id,chave_idempotencia) where not is_deleted;
create index if not exists ix_orcamento_liquidacao_emp
  on sigov.orcamento_liquidacao(empenho_id) where not is_deleted;
create index if not exists ix_orcamento_liquidacao_ctx
  on sigov.orcamento_liquidacao(tenant_id,exercicio_id) where not is_deleted;

create table if not exists sigov.orcamento_liquidacao_item(
  id bigint generated always as identity primary key,
  liquidacao_id bigint not null references sigov.orcamento_liquidacao(id),
  empenho_item_id bigint not null references sigov.orcamento_empenho_item(id),
  valor numeric(18,2) not null check (valor > 0),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now()
);

create index if not exists ix_orcamento_liq_item_liq
  on sigov.orcamento_liquidacao_item(liquidacao_id) where not is_deleted;
create index if not exists ix_orcamento_liq_item_empitem
  on sigov.orcamento_liquidacao_item(empenho_item_id) where not is_deleted;

-- ----------------------------------------------------------------------------
-- 5) Receita prevista e arrecadada (base real das apurações LRF/RREO):
--    lançamento tipado e idempotente de arrecadação por linha prevista.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_receita_prevista(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  codigo varchar(40) not null,
  nome varchar(200) not null,
  natureza varchar(30) not null
    check (natureza in ('TRIBUTARIA','TRANSFERENCIAS','PATRIMONIAL','SERVICIOS','DIVERSAS_RECEITAS')),
  valor_prevista numeric(18,2) not null check (valor_prevista >= 0),
  valor_arrecadado numeric(18,2) not null default 0 check (valor_arrecadado >= 0),
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_receita_codigo
  on sigov.orcamento_receita_prevista(tenant_id,entidade_id,exercicio_id,codigo) where not is_deleted;
create index if not exists ix_orcamento_receita_ctx
  on sigov.orcamento_receita_prevista(tenant_id,exercicio_id) where not is_deleted;

create table if not exists sigov.orcamento_receita_arrecadacao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  receita_id bigint not null references sigov.orcamento_receita_prevista(id),
  valor numeric(18,2) not null check (valor > 0),
  data_arrecadacao date not null,
  historico text not null,
  chave_idempotencia varchar(80) not null,
  criado_em timestamptz not null default now(),
  criado_por bigint
);

create unique index if not exists ux_orcamento_arrecadacao_chave
  on sigov.orcamento_receita_arrecadacao(tenant_id,chave_idempotencia);
create index if not exists ix_orcamento_arrecadacao_receita
  on sigov.orcamento_receita_arrecadacao(receita_id);

-- ----------------------------------------------------------------------------
-- 6) Limites LRF por esfera (BR-13.5): catalogo orcamento_limite_lrf com
--    linhas institucionais por esfera (tenant null) e override por tenant
--    (tenant preenchido tem precedência). Semantics: MAX = teto
--    (percentual nao pode passar do alvo; alerta_pct e um ponto abaixo;
--    bloqueio_pct, quando preenchido, nega empenho novo ao ultrapassar);
--    MIN = piso (percentual nao pode ficar abaixo do alvo - ex.: MDE 25%).
--    Nenhum percentual existe fora deste catalogo (regras 11/22).
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_limite_lrf(
  id bigint generated always as identity primary key,
  tenant_id bigint null,
  esfera_governo varchar(10) not null check (esfera_governo in ('municipal','estadual','federal')),
  chave varchar(60) not null,
  nome varchar(160) not null,
  tipo_comparacao varchar(4) not null check (tipo_comparacao in ('MAX','MIN')),
  valor_alvo numeric(7,4) not null default 0 check (valor_alvo >= 0),
  alerta_pct numeric(7,4) not null default 0 check (alerta_pct >= 0),
  bloqueio_pct numeric(7,4) null check (bloqueio_pct is null or bloqueio_pct >= 0),
  base_receita varchar(10) not null default 'PREVISTA' check (base_receita in ('PREVISTA','ARRECADADA')),
  ativo boolean not null default true,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint
);

create unique index if not exists ux_orcamento_limite_lrf
  on sigov.orcamento_limite_lrf(coalesce(tenant_id,0),esfera_governo,chave) where not is_deleted;

insert into sigov.orcamento_limite_lrf(esfera_governo,chave,nome,tipo_comparacao,valor_alvo,alerta_pct,bloqueio_pct)
select v.esfera,v.chave,v.nome,v.tipo,v.alvo,v.alerta,v.bloqueio from (values
  ('municipal','PESSOAL_EXECUTIVO_MAX','Despesa total com pessoal - Poder Executivo (LC 101/2000 art. 20 II)                ','MAX',50.0000,45.0000,50.0000),
  ('municipal','PESSOAL_LEGISLATIVO_MAX','Despesa total com pessoal - Legislativo (LC 101/2000 art. 19 § 1o)               ','MAX',25.0000,20.0000,25.0000),
  ('municipal','PESSOAL_JUDICIARIO_MAX','Despesa total com pessoal - MP/Tribunais (LC 101/2000 art. 20 § 4o)                ','MAX',15.0000,12.0000,15.0000),
  ('municipal','RESTOS_A_PAGAR_MAX','Restos a pagar em execucao sobre a receita do exercicio (limite organico parametrizado)','MAX',30.0000,25.0000,30.0000),
  ('municipal','FUNC_SOCIAL_MIN','Minimo de despesa em funcoes sociais (lei organica parametrizada; zero = desativado)     ','MIN',0.0000,0.0000,null),
  ('municipal','MDE_MIN','Minimo constitucional de saude e educacao (CF/88 art. 212 - 25%)                            ','MIN',25.0000,24.0000,23.0000),
  ('estadual','PESSOAL_EXECUTIVO_MAX','Despesa total com pessoal - Poder Executivo (LC 101/2000 art. 20 II)                ','MAX',50.0000,45.0000,50.0000),
  ('estadual','PESSOAL_LEGISLATIVO_MAX','Despesa total com pessoal - Legislativo (LC 101/2000 art. 19 § 1o)                 ','MAX',25.0000,20.0000,25.0000),
  ('estadual','PESSOAL_JUDICIARIO_MAX','Despesa total com pessoal - MP/Tribunais (LC 101/2000 art. 20 § 4o)                ','MAX',15.0000,12.0000,15.0000),
  ('estadual','RESTOS_A_PAGAR_MAX','Restos a pagar em execucao sobre a receita do exercicio (limite organico parametrizado)','MAX',30.0000,25.0000,30.0000),
  ('estadual','FUNC_SOCIAL_MIN','Minimo de despesa em funcoes sociais (leis estaduais/organicas; zero = desativado)         ','MIN',0.0000,0.0000,null),
  ('estadual','MDE_MIN','Minimo constitucional de saude e educacao (CF/88 art. 212 - 25%)                            ','MIN',25.0000,24.0000,23.0000),
  ('federal','PESSOAL_EXECUTIVO_MAX','Despesa total com pessoal - Poder Executivo (LC 101/2000 art. 20 II)                ','MAX',50.0000,45.0000,50.0000),
  ('federal','PESSOAL_LEGISLATIVO_MAX','Despesa total com pessoal - Legislativo (LC 101/2000 art. 19 § 1o)                  ','MAX',25.0000,20.0000,25.0000),
  ('federal','PESSOAL_JUDICIARIO_MAX','Despesa total com pessoal - MP/Tribunais (LC 101/2000 art. 20 § 4o)                 ','MAX',15.0000,12.0000,15.0000),
  ('federal','RESTOS_A_PAGAR_MAX','Restos a pagar em execucao sobre a receita do exercicio (limite organico parametrizado)','MAX',30.0000,25.0000,30.0000),
  ('federal','FUNC_SOCIAL_MIN','Minimo de despesa em funcoes sociais (parametrizado; zero = desativado)                   ','MIN',0.0000,0.0000,null),
  ('federal','MDE_MIN','Minimo constitucional de saude e educacao (federal sem minimo constitucional = zero; parametrizavel)','MIN',0.0000,0.0000,null)
) v(esfera,chave,nome,tipo,alvo,alerta,bloqueio)
where not exists(select 1 from sigov.orcamento_limite_lrf l
                  where l.tenant_id is null and l.esfera_governo=v.esfera and l.chave=v.chave and not l.is_deleted);

-- ----------------------------------------------------------------------------
-- 7) Apuração LRF (BR-13.5): histórico append-only de snapshots calculados
--    SOMENTE dos dados reais do exercício (empenhos, liquidações, receitas,
--    dotações). Cada reapuração incrementa versao por chave; o motor lê a
--    última versão e a regra de bloqueio do empenho novo usa o percentual
--    pós-mudada (na mesma transação). Sem catálogo mock (regra 12).
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_lrf_apuracao(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  esfera_governo varchar(10) not null check (esfera_governo in ('municipal','estadual','federal')),
  periodo date not null,
  limite_chave varchar(60) not null,
  base_valor numeric(18,2) not null default 0,
  valor_atual numeric(18,2) not null default 0,
  percentual numeric(9,4) not null default 0,
  situacao varchar(10) not null check (situacao in ('DENTRO','ALERTA','BLOQUEIO')),
  detalhe text not null default '',
  versao integer not null default 1,
  criado_em timestamptz not null default now(),
  criado_por bigint
);

create index if not exists ix_orcamento_lrf_apuracao
  on sigov.orcamento_lrf_apuracao(tenant_id,entidade_id,exercicio_id,limite_chave,versao desc);

-- ----------------------------------------------------------------------------
-- 8) Previsão bimestral (LC 101/2000 art. 51, BR-13.6): gerada automaticamente
--    a partir dos dados reais (receitas previstas/arrecadadas e dotações/
--    empenhos/liquidações do exercício); 1 VIGENTE por tenant x exercicio x
--    bimestre - regenerar substitui a anterior mantendo-a auditável.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_previsao_bimestral(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  bimestre integer not null check (bimestre between 1 and 6),
  receita_prevista_total numeric(18,2) not null check (receita_prevista_total >= 0),
  receita_arrecadada_total numeric(18,2) not null check (receita_arrecadada_total >= 0),
  despesa_autorizada_total numeric(18,2) not null check (despesa_autorizada_total >= 0),
  despesa_empenhada_total numeric(18,2) not null check (despesa_empenhada_total >= 0),
  despesa_liquidada_total numeric(18,2) not null check (despesa_liquidada_total >= 0),
  saldo_receitas numeric(18,2) not null,
  dados_fonte jsonb not null default '{}'::jsonb,
  vigente boolean not null default true,
  gerado_em timestamptz not null default now(),
  gerado_por bigint,
  observacao text
);

create unique index if not exists ux_orcamento_previsao_vigente
  on sigov.orcamento_previsao_bimestral(tenant_id,entidade_id,exercicio_id,bimestre) where vigente;
create index if not exists ix_orcamento_previsao_ctx
  on sigov.orcamento_previsao_bimestral(tenant_id,exercicio_id);

-- ----------------------------------------------------------------------------
-- 9) RREO (LC 101/2000 art. 54, BR-13.7): lote semestral/anual com conjuntos
--    mínimos calculados dos dados reais; 1 VIGENTE por tenant x exercicio x
--    tipo x semestre - regenerar veta o anterior e mantém histórico.
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_rreo_lote(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  tipo varchar(10) not null check (tipo in ('SEMESTRAL','ANUAL')),
  semestre integer null check (semestre is null or semestre in (1,2)),
  periodo_fim date not null,
  vigente boolean not null default true,
  gerado_em timestamptz not null default now(),
  gerado_por bigint
);

create unique index if not exists ux_orcamento_rreo_vigente
  on sigov.orcamento_rreo_lote(tenant_id,entidade_id,exercicio_id,tipo,coalesce(semestre,0)) where vigente;
create index if not exists ix_orcamento_rreo_ctx
  on sigov.orcamento_rreo_lote(tenant_id,exercicio_id);

create table if not exists sigov.orcamento_rreo_item(
  id bigint generated always as identity primary key,
  lote_id bigint not null references sigov.orcamento_rreo_lote(id),
  conjunto varchar(60) not null,
  valor numeric(18,2) not null,
  detalhe text not null default ''
);

create unique index if not exists ux_orcamento_rreo_item
  on sigov.orcamento_rreo_item(lote_id,conjunto);

-- ----------------------------------------------------------------------------
-- 10) Estado do exercício (BR-13.8): ENCERRADO bloqueia empenho/liquidação/
--     crédito novos (EXERCICIO_ENCERRADO, 409). Ausência de linha = ABERTO.
--     Reabertura exige justificativa (auditoria completa).
-- ----------------------------------------------------------------------------
create table if not exists sigov.orcamento_estado_exercicio(
  id bigint generated always as identity primary key,
  tenant_id bigint not null,
  entidade_id bigint not null,
  exercicio_id bigint not null references sigov.exercicio(id),
  situacao varchar(10) not null check (situacao in ('ABERTO','ENCERRADO')),
  encerrado_em timestamptz null,
  encerrado_por bigint,
  encerramento_motivo text null,
  reaberto_em timestamptz null,
  reaberto_por bigint,
  reabertura_justificativa text null,
  is_deleted boolean not null default false,
  created_at timestamptz not null default now(),
  created_by bigint,
  updated_at timestamptz,
  updated_by bigint,
  deleted_at timestamptz
);

create unique index if not exists ux_orcamento_estado_exercicio
  on sigov.orcamento_estado_exercicio(exercicio_id) where not is_deleted;
create index if not exists ix_orcamento_estado_exerc_ctx
  on sigov.orcamento_estado_exercicio(tenant_id,entidade_id) where not is_deleted;

-- ----------------------------------------------------------------------------
-- 11) Parametros da frente (catalogo sigov.parametro_modulo + override por
--     tenant em sigov.parametro_modulo_valor - regra 11/22).
-- ----------------------------------------------------------------------------
insert into sigov.parametro_modulo(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo,is_deleted)
select v.modulo,v.codigo,v.nome,v.descricao,v.tipo,(v.valor_padrao)::jsonb,v.sensivel,v.ordem,v.ativo,v.is_deleted from (values
  ('ORCAMENTO','ORCAMENTO_APURO_LRF_AUTO','Apuracao LRF automatica apos movimentos',
   'true = apura os limites da esfera apos cada empenho/liquidacao/arrecadacao; false = somente via comando explicito orcamento.lrf.apurar.',
   'BOOLEAN','true',false,1,true,false),
  ('ORCAMENTO','ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES','Restos a pagar de antevedentes na apuracao',
   'true = o monitor RESTOS_A_PAGAR_MAX considera empenhos nao liquidados de exercicios anteriores (LC 101/2000 art. 36 e ss).',
   'BOOLEAN','true',false,2,true,false),
  ('ORCAMENTO','ORCAMENTO_CREDITO_JUSTIFICATIVA_MIN_CARACTERES','Tamanho minimo da justificativa de credito adicional',
   'LC 101/2000 art. 41: credito exigido para fazer face a despesa exige justificativa especifica. Zero = desativado.',
   'DECIMAL','10',false,3,true,false),
  ('ORCAMENTO','ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES','Tamanho minimo da justificativa de anulacao',
   'Anulacao de empenho ou liquidacao exige justificativa registrada (trilhas auditorias). Zero = desativado.',
   'DECIMAL','5',false,4,true,false),
  ('ORCAMENTO','ORCAMENTO_REABERTURA_JUSTIFICATIVA_MIN_CARACTERES','Tamanho minimo da justificativa de reabertura de exercicio',
   'Reabrir exercicio encerrado exige justificativa (brasil: Lei 4.320/64 arts. 36-38; trilhas auditorias). Zero = desativado.',
   'DECIMAL','10',false,5,true,false)
) v(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo,is_deleted)
where not exists(select 1 from sigov.parametro_modulo p where p.modulo=v.modulo and p.codigo=v.codigo and not p.is_deleted);

-- ----------------------------------------------------------------------------
-- 12) Catalogo de permissoes da frente (chave canonica por ato - regra 11/12)
--     + vinculo do perfil sistemico SUPERADMIN (padrao documentado).
-- ----------------------------------------------------------------------------
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'orcamento',v.chave,v.recurso,v.acao,v.descricao,true,false from (values
  ('orcamento.visualizar','orcamento','visualizar','Visualizar estrutura, dotacoes, empenhos, liquidacoes, receitas, apuracoes LRF, prever e dashboards da estacao de orcamento - BR-13.x'),
  ('orcamento.estrutura.criar','orcamento.estrutura','criar','Criar/editar programas e acoes (projeto/atividade/subprojeto) do exercicio; funcao/subfuncao sao catalogos nacionais globais - BR-13.1'),
  ('orcamento.dotacao.criar','orcamento.dotacao','criar','Criar linhas de dotacao por acao x fonte (dotacao inicial; unica por linha) - BR-13.1/13.2'),
  ('orcamento.credito.registrar','orcamento.credito','registrar','Registrar credito adicional (suplementar/especial/extraordinario) com ato legal e justificativa (minimo parametrico) - BR-13.2'),
  ('orcamento.empenho.abrir','orcamento.empenho','abrir','Abrir empenho CP com itens por dotacao; valida disponibilidade suficiente em todos os itens e o teto LRF aplicavel - BR-13.3'),
  ('orcamento.empenho.anular','orcamento.empenho','anular','Anular empenho (total/parcial) devolvendo disponibilidade; exige justificativa parametrica - BR-13.3'),
  ('orcamento.liquidacao.registrar','orcamento.liquidacao','registrar','Registrar liquidacao SL sobre empenho disponivel por item (LIQUIDACAO_SUPER_EMPENHO quando excede) - BR-13.4'),
  ('orcamento.liquidacao.anular','orcamento.liquidacao','anular','Anular liquidacao (total/parcial) devolvendo empenho disponivel; exige justificativa - BR-13.4'),
  ('orcamento.receita.arrecadar','orcamento.receita','arrecadar','Lancar arrecadacao sobre linha de receita prevista (idempotente por chave) - base real das apuracoes - BR-13.5/13.6'),
  ('orcamento.lrf.apurar','orcamento.lrf','apurar','Apurar limites LRF da esfera a partir dos dados reais do exercicio (historico versionado por chave) - BR-13.5'),
  ('orcamento.previsao.gerar','orcamento.previsao','gerar','Gerar previsao bimestral automatica (LC 101/2000 art. 51) substituindo a VIGENTE anterior - BR-13.6'),
  ('orcamento.rreo.gerar','orcamento.rreo','gerar','Gerar RREO semestral/anual (LC 101/2000 art. 54) com conjuntos minimos dos dados reais - BR-13.7'),
  ('orcamento.exercicio.encerrar','orcamento.exercicio','encerrar','Encerrar exercicio (bloqueia empenho/liquidacao/credito novos) e reabri-lo com justificativa - BR-13.8')
) v(chave,recurso,acao,descricao) where not exists(select 1 from sigov.permissao p where p.chave=v.chave);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false
  from sigov.perfil_acesso pa
  cross join sigov.permissao p
 where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
   and p.modulo='orcamento' and p.ativo and not p.is_deleted
   and p.chave in ('orcamento.visualizar','orcamento.estrutura.criar','orcamento.dotacao.criar',
    'orcamento.credito.registrar','orcamento.empenho.abrir','orcamento.empenho.anular',
    'orcamento.liquidacao.registrar','orcamento.liquidacao.anular','orcamento.receita.arrecadar',
    'orcamento.lrf.apurar','orcamento.previsao.gerar','orcamento.rreo.gerar','orcamento.exercicio.encerrar')
on conflict (perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;

-- C2S13-POSTCOND: falha explicita se faltar qualquer artefato (regra 13)
do $$
declare
  n_tabelas int;
  n_indices int;
  n_chaves int;
  n_perfis int;
  n_parametros int;
  n_funcoes int;
  n_subfuncoes int;
  n_limites int;
begin
  select count(1) into n_tabelas
    from information_schema.tables
   where table_schema='sigov' and table_name in (
     'orcamento_funcao','orcamento_subfuncao','orcamento_programa','orcamento_acao',
     'orcamento_dotacao','orcamento_credito_adicional','orcamento_empenho','orcamento_empenho_item',
     'orcamento_liquidacao','orcamento_liquidacao_item','orcamento_receita_prevista','orcamento_receita_arrecadacao','orcamento_limite_lrf',
     'orcamento_lrf_apuracao','orcamento_previsao_bimestral','orcamento_rreo_lote','orcamento_rreo_item',
     'orcamento_estado_exercicio');
  select count(1) into n_indices
    from pg_indexes
   where schemaname='sigov' and indexname in (
     'ux_orcamento_programa_codigo','ix_orcamento_programa_ctx','ux_orcamento_acao_codigo',
     'ix_orcamento_acao_ctx','ix_orcamento_acao_programa','ux_orcamento_dotacao_linha',
     'ix_orcamento_dotacao_ctx','ux_orcamento_credito_chave','ix_orcamento_credito_ctx',
     'ix_orcamento_credito_dotacao','ux_orcamento_empenho_chave','ix_orcamento_empenho_ctx',
     'ix_orcamento_empenho_fornecedor','ix_orcamento_empenho_item_emp','ix_orcamento_empenho_item_dota',
     'ux_orcamento_liquidacao_chave','ix_orcamento_liquidacao_emp','ix_orcamento_liquidacao_ctx',
     'ix_orcamento_liq_item_liq','ix_orcamento_liq_item_empitem','ux_orcamento_receita_codigo',
     'ix_orcamento_receita_ctx','ux_orcamento_arrecadacao_chave','ix_orcamento_arrecadacao_receita','ux_orcamento_limite_lrf','ix_orcamento_lrf_apuracao',
     'ux_orcamento_previsao_vigente','ix_orcamento_previsao_ctx','ux_orcamento_rreo_vigente',
     'ix_orcamento_rreo_ctx','ux_orcamento_rreo_item','ux_orcamento_estado_exercicio',
     'ix_orcamento_estado_exerc_ctx');
  select count(1) into n_chaves
    from sigov.permissao
   where modulo='orcamento' and ativo and not is_deleted
     and chave in ('orcamento.visualizar','orcamento.estrutura.criar','orcamento.dotacao.criar',
      'orcamento.credito.registrar','orcamento.empenho.abrir','orcamento.empenho.anular',
      'orcamento.liquidacao.registrar','orcamento.liquidacao.anular','orcamento.receita.arrecadar',
      'orcamento.lrf.apurar','orcamento.previsao.gerar','orcamento.rreo.gerar','orcamento.exercicio.encerrar');
  select count(1) into n_perfis
    from sigov.perfil_permissao pp
    join sigov.perfil_acesso pa on pa.id=pp.perfil_acesso_id
    join sigov.permissao p on p.id=pp.permissao_id
   where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
     and p.modulo='orcamento' and pp.ativo and not pp.is_deleted;
  select count(1) into n_parametros
    from sigov.parametro_modulo
   where modulo='ORCAMENTO' and ativo and not is_deleted;
  select count(1) into n_funcoes from sigov.orcamento_funcao where not is_deleted;
  select count(1) into n_subfuncoes from sigov.orcamento_subfuncao where not is_deleted;
  select count(1) into n_limites from sigov.orcamento_limite_lrf where tenant_id is null and not is_deleted;
  if n_tabelas<>18 then raise exception 'C2S13-POSTCOND falhou: tabelas=% (esperado 18)',n_tabelas; end if;
  if n_indices<>33 then raise exception 'C2S13-POSTCOND falhou: indices=% (esperado 33)',n_indices; end if;
  if n_chaves<>13 then raise exception 'C2S13-POSTCOND falhou: chaves de permissao=% (esperado 13)',n_chaves; end if;
  if n_perfis <> 13*(select count(1) from sigov.perfil_acesso pa2 where pa2.codigo_externo='SUPERADMIN' and pa2.ativo and not pa2.is_deleted) then raise exception 'C2S13-POSTCOND falhou: vinculos SUPERADMIN=% (esperado 13 por perfil SUPERADMIN ativo)',n_perfis; end if;
  if n_parametros<5 then raise exception 'C2S13-POSTCOND falhou: parametros=% (esperado >=5)',n_parametros; end if;
  if n_funcoes<19 then raise exception 'C2S13-POSTCOND falhou: funcoes=% (esperado >=19)',n_funcoes; end if;
  if n_subfuncoes<9 then raise exception 'C2S13-POSTCOND falhou: subfuncoes=% (esperado >=9)',n_subfuncoes; end if;
  if n_limites<>18 then raise exception 'C2S13-POSTCOND falhou: limites institucionais=% (esperado 18)',n_limites; end if;
  raise notice 'RC-EVO-C2S13 concluida: dominio tipado orcamento_* com estrutura Lei 4.320/64, limites LRF por esfera, previsao bimestral, RREO e exercicio fechado (BR-13.1 a BR-13.8).';
end $$;
