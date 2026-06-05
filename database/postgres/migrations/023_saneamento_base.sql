-- Saneamento base - schema único sigov, PostgreSQL, Dapper-ready e idempotente.
create extension if not exists pgcrypto;

create table if not exists sigov.saneamento_consumidor (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    pessoa_id bigint not null references sigov.pessoa(id),
    codigo_consumidor varchar(80) not null,
    tipo_consumidor varchar(40) not null,
    situacao varchar(40) not null,
    data_cadastro date not null default current_date,
    observacao text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    deleted_at timestamptz null,
    deleted_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo_consumidor),
    unique (tenant_id, entidade_id, pessoa_id)
);

create table if not exists sigov.saneamento_ligacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint not null references sigov.entidade(id),
    consumidor_id bigint not null references sigov.saneamento_consumidor(id),
    numero_ligacao varchar(80) not null,
    tipo_ligacao varchar(40) not null,
    situacao varchar(40) not null,
    data_ligacao date null,
    data_corte date null,
    categoria varchar(80) not null,
    economia int not null default 1,
    observacao text null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero_ligacao)
);

create table if not exists sigov.saneamento_unidade_consumidora (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    consumidor_id bigint not null references sigov.saneamento_consumidor(id), ligacao_id bigint null references sigov.saneamento_ligacao(id),
    codigo_unidade varchar(80) not null, endereco_json jsonb not null default '{}'::jsonb,
    bairro varchar(120) null, setor varchar(80) null, rota varchar(80) null, sequencia_rota int null,
    latitude numeric(12,8) null, longitude numeric(12,8) null, situacao varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo_unidade)
);

create table if not exists sigov.saneamento_hidrometro (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    unidade_consumidora_id bigint not null references sigov.saneamento_unidade_consumidora(id),
    numero_serie varchar(100) not null, marca varchar(100) null, modelo varchar(100) null, diametro varchar(40) null,
    data_instalacao date null, data_retirada date null, leitura_instalacao numeric(18,4) not null default 0, situacao varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero_serie)
);

create table if not exists sigov.saneamento_leitura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    unidade_consumidora_id bigint not null references sigov.saneamento_unidade_consumidora(id), hidrometro_id bigint null references sigov.saneamento_hidrometro(id),
    competencia varchar(7) not null, data_leitura date not null, leitura_anterior numeric(18,4) not null default 0, leitura_atual numeric(18,4) not null,
    consumo_medido numeric(18,4) not null, consumo_faturado numeric(18,4) not null, tipo_leitura varchar(40) not null, ocorrencia varchar(150) null,
    leitor_usuario_id bigint null references sigov.usuario(id), latitude numeric(12,8) null, longitude numeric(12,8) null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, unidade_consumidora_id, competencia),
    check (consumo_medido = leitura_atual - leitura_anterior), check (consumo_faturado >= 0)
);

create table if not exists sigov.saneamento_fatura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    unidade_consumidora_id bigint not null references sigov.saneamento_unidade_consumidora(id), leitura_id bigint null references sigov.saneamento_leitura(id),
    numero varchar(80) not null, competencia varchar(7) not null, data_emissao date not null default current_date, data_vencimento date not null,
    valor_agua numeric(18,2) not null default 0, valor_esgoto numeric(18,2) not null default 0, valor_servicos numeric(18,2) not null default 0,
    valor_multa numeric(18,2) not null default 0, valor_juros numeric(18,2) not null default 0, valor_desconto numeric(18,2) not null default 0,
    valor_total numeric(18,2) not null, valor_pago numeric(18,2) not null default 0, status varchar(40) not null,
    codigo_barras varchar(150) null, linha_digitavel varchar(150) null, ambiente varchar(30) not null default 'DEVELOPMENT',
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero),
    check (status in ('ABERTA','PAGA','VENCIDA','CANCELADA','PARCELADA')), check (valor_total >= 0), check (valor_pago >= 0)
);

create table if not exists sigov.saneamento_fatura_item (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    fatura_id bigint not null references sigov.saneamento_fatura(id), tipo_item varchar(80) not null, descricao varchar(250) not null,
    quantidade numeric(18,4) not null default 1, valor_unitario numeric(18,4) not null default 0, valor_total numeric(18,2) not null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.saneamento_arrecadacao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    fatura_id bigint not null references sigov.saneamento_fatura(id), numero varchar(80) not null, data_pagamento date not null, forma_pagamento varchar(40) not null,
    valor_pago numeric(18,2) not null, origem varchar(80) null, metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero)
);

create table if not exists sigov.saneamento_parcelamento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    consumidor_id bigint not null references sigov.saneamento_consumidor(id), numero varchar(80) not null, data_parcelamento date not null default current_date,
    quantidade_parcelas int not null, valor_total numeric(18,2) not null, status varchar(40) not null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero), check (quantidade_parcelas > 0)
);

create table if not exists sigov.saneamento_parcelamento_item (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    parcelamento_id bigint not null references sigov.saneamento_parcelamento(id), numero_parcela int not null, data_vencimento date not null,
    valor_parcela numeric(18,2) not null, status varchar(40) not null default 'ABERTA', fatura_id bigint null references sigov.saneamento_fatura(id),
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, parcelamento_id, numero_parcela)
);

create table if not exists sigov.saneamento_equipe_campo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(80) not null, nome varchar(150) not null, responsavel_usuario_id bigint null references sigov.usuario(id), membros_json jsonb not null default '[]'::jsonb,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo)
);

create table if not exists sigov.saneamento_ordem_servico (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    unidade_consumidora_id bigint null references sigov.saneamento_unidade_consumidora(id), consumidor_id bigint null references sigov.saneamento_consumidor(id),
    numero varchar(80) not null, tipo_servico varchar(100) not null, prioridade varchar(40) not null, status varchar(40) not null,
    data_abertura timestamptz not null default now(), data_agendamento timestamptz null, data_execucao timestamptz null,
    equipe_campo_id bigint null references sigov.saneamento_equipe_campo(id), solicitante_pessoa_id bigint null references sigov.pessoa(id),
    descricao text not null, solucao text null, latitude numeric(12,8) null, longitude numeric(12,8) null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero),
    check (status in ('ABERTA','AGENDADA','EM_CAMPO','EXECUTADA','CANCELADA','NAO_EXECUTADA'))
);

create table if not exists sigov.saneamento_servico_executado (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    ordem_servico_id bigint not null references sigov.saneamento_ordem_servico(id), tipo_servico varchar(100) not null, descricao text not null,
    data_execucao timestamptz not null default now(), equipe_campo_id bigint null references sigov.saneamento_equipe_campo(id), materiais_json jsonb not null default '[]'::jsonb, evidencias_json jsonb not null default '[]'::jsonb,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.saneamento_laboratorio_amostra (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    numero varchar(80) not null, ponto_coleta varchar(250) not null, data_coleta timestamptz not null, coletado_por_usuario_id bigint null references sigov.usuario(id),
    latitude numeric(12,8) null, longitude numeric(12,8) null, status varchar(40) not null, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, numero)
);

create table if not exists sigov.saneamento_laboratorio_resultado (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    amostra_id bigint not null references sigov.saneamento_laboratorio_amostra(id), parametro varchar(150) not null, valor varchar(100) not null, unidade varchar(50) null,
    limite_referencia varchar(100) null, conforme boolean null, data_resultado date not null default current_date, observacao text null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.saneamento_rede_trecho (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
    codigo varchar(80) not null, tipo_rede varchar(80) not null, material varchar(80) null, diametro varchar(40) null, extensao_metros numeric(18,4) null, geometria_geojson jsonb null, situacao varchar(40) not null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null,
    unique (tenant_id, entidade_id, codigo)
);

create table if not exists sigov.saneamento_ocorrencia_operacional (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    tipo_ocorrencia varchar(100) not null, descricao text not null, data_ocorrencia timestamptz not null default now(),
    unidade_consumidora_id bigint null references sigov.saneamento_unidade_consumidora(id), rede_trecho_id bigint null references sigov.saneamento_rede_trecho(id), ordem_servico_id bigint null references sigov.saneamento_ordem_servico(id),
    status varchar(40) not null, latitude numeric(12,8) null, longitude numeric(12,8) null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create table if not exists sigov.saneamento_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), exercicio_id bigint null references sigov.exercicio(id),
    tipo_evento varchar(120) not null, aggregate_type varchar(120) not null, aggregate_id bigint null, payload_json jsonb not null default '{}'::jsonb,
    status varchar(40) not null default 'PENDENTE', tentativas int not null default 0, processado_at timestamptz null,
    ativo boolean not null default true, is_deleted boolean not null default false,
    created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, correlation_id uuid null
);

create index if not exists idx_san_consumidor_tenant_codigo on sigov.saneamento_consumidor (tenant_id, entidade_id, codigo_consumidor) where is_deleted = false;
create index if not exists idx_san_consumidor_tenant_pessoa on sigov.saneamento_consumidor (tenant_id, entidade_id, pessoa_id) where is_deleted = false;
create index if not exists idx_san_ligacao_tenant_numero on sigov.saneamento_ligacao (tenant_id, entidade_id, numero_ligacao) where is_deleted = false;
create index if not exists idx_san_unidade_tenant_codigo on sigov.saneamento_unidade_consumidora (tenant_id, entidade_id, codigo_unidade) where is_deleted = false;
create index if not exists idx_san_unidade_tenant_rota on sigov.saneamento_unidade_consumidora (tenant_id, entidade_id, rota, sequencia_rota) where is_deleted = false;
create index if not exists idx_san_hidrometro_tenant_serie on sigov.saneamento_hidrometro (tenant_id, entidade_id, numero_serie) where is_deleted = false;
create index if not exists idx_san_leitura_tenant_competencia on sigov.saneamento_leitura (tenant_id, entidade_id, competencia) where is_deleted = false;
create index if not exists idx_san_leitura_tenant_unidade on sigov.saneamento_leitura (tenant_id, entidade_id, unidade_consumidora_id) where is_deleted = false;
create index if not exists idx_san_fatura_tenant_numero on sigov.saneamento_fatura (tenant_id, entidade_id, numero) where is_deleted = false;
create index if not exists idx_san_fatura_tenant_status on sigov.saneamento_fatura (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_san_fatura_tenant_unidade on sigov.saneamento_fatura (tenant_id, entidade_id, unidade_consumidora_id) where is_deleted = false;
create index if not exists idx_san_arrecadacao_tenant_fatura on sigov.saneamento_arrecadacao (tenant_id, entidade_id, fatura_id) where is_deleted = false;
create index if not exists idx_san_parcelamento_tenant_consumidor on sigov.saneamento_parcelamento (tenant_id, entidade_id, consumidor_id) where is_deleted = false;
create index if not exists idx_san_os_tenant_numero on sigov.saneamento_ordem_servico (tenant_id, entidade_id, numero) where is_deleted = false;
create index if not exists idx_san_os_tenant_status on sigov.saneamento_ordem_servico (tenant_id, entidade_id, status) where is_deleted = false;
create index if not exists idx_san_os_tenant_unidade on sigov.saneamento_ordem_servico (tenant_id, entidade_id, unidade_consumidora_id) where is_deleted = false;
create index if not exists idx_san_equipe_tenant_codigo on sigov.saneamento_equipe_campo (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_san_lab_amostra_tenant_numero on sigov.saneamento_laboratorio_amostra (tenant_id, entidade_id, numero) where is_deleted = false;
create index if not exists idx_san_lab_resultado_tenant_amostra on sigov.saneamento_laboratorio_resultado (tenant_id, entidade_id, amostra_id) where is_deleted = false;
create index if not exists idx_san_rede_tenant_codigo on sigov.saneamento_rede_trecho (tenant_id, entidade_id, codigo) where is_deleted = false;
create index if not exists idx_san_ocorrencia_tenant_status on sigov.saneamento_ocorrencia_operacional (tenant_id, entidade_id, status) where is_deleted = false;

create or replace view sigov.vw_saneamento_dashboard as
select c.tenant_id, c.entidade_id, count(distinct c.id) as total_consumidores,
       count(distinct uc.id) as total_unidades_consumidoras,
       count(distinct l.id) filter (where l.situacao = 'ATIVA') as ligacoes_ativas,
       count(distinct h.id) filter (where h.situacao = 'INSTALADO') as hidrometros_ativos,
       count(distinct f.id) filter (where f.status = 'ABERTA') as faturas_abertas,
       count(distinct f.id) filter (where f.status = 'VENCIDA') as faturas_vencidas,
       coalesce(sum(f.valor_total) filter (where date_trunc('month', f.data_emissao) = date_trunc('month', current_date)),0) as valor_faturado_mes,
       coalesce(sum(a.valor_pago) filter (where date_trunc('month', a.data_pagamento) = date_trunc('month', current_date)),0) as valor_arrecadado_mes,
       count(distinct os.id) filter (where os.status = 'ABERTA') as ordens_abertas,
       count(distinct os.id) filter (where os.status = 'EM_CAMPO') as ordens_em_campo,
       count(distinct la.id) filter (where la.status <> 'CONCLUIDA') as amostras_pendentes,
       count(distinct rt.id) as trechos_rede_cadastrados
from sigov.saneamento_consumidor c
left join sigov.saneamento_unidade_consumidora uc on uc.tenant_id=c.tenant_id and uc.entidade_id=c.entidade_id and uc.consumidor_id=c.id and uc.is_deleted=false
left join sigov.saneamento_ligacao l on l.tenant_id=c.tenant_id and l.entidade_id=c.entidade_id and l.consumidor_id=c.id and l.is_deleted=false
left join sigov.saneamento_hidrometro h on h.tenant_id=c.tenant_id and h.entidade_id=c.entidade_id and h.unidade_consumidora_id=uc.id and h.is_deleted=false
left join sigov.saneamento_fatura f on f.tenant_id=c.tenant_id and f.entidade_id=c.entidade_id and f.unidade_consumidora_id=uc.id and f.is_deleted=false
left join sigov.saneamento_arrecadacao a on a.tenant_id=c.tenant_id and a.entidade_id=c.entidade_id and a.fatura_id=f.id and a.is_deleted=false
left join sigov.saneamento_ordem_servico os on os.tenant_id=c.tenant_id and os.entidade_id=c.entidade_id and os.consumidor_id=c.id and os.is_deleted=false
left join sigov.saneamento_laboratorio_amostra la on la.tenant_id=c.tenant_id and la.entidade_id=c.entidade_id and la.is_deleted=false
left join sigov.saneamento_rede_trecho rt on rt.tenant_id=c.tenant_id and rt.entidade_id=c.entidade_id and rt.is_deleted=false
where c.is_deleted=false
group by c.tenant_id, c.entidade_id;

create or replace view sigov.vw_saneamento_faturamento_resumo as select tenant_id, entidade_id, competencia, status, count(*) total, sum(valor_total) valor_total from sigov.saneamento_fatura where is_deleted=false group by tenant_id, entidade_id, competencia, status;
create or replace view sigov.vw_saneamento_arrecadacao_resumo as select tenant_id, entidade_id, data_pagamento, count(*) total, sum(valor_pago) valor_pago from sigov.saneamento_arrecadacao where is_deleted=false group by tenant_id, entidade_id, data_pagamento;
create or replace view sigov.vw_saneamento_inadimplencia_resumo as select tenant_id, entidade_id, count(*) total_faturas, sum(valor_total - valor_pago) saldo from sigov.saneamento_fatura where is_deleted=false and status in ('ABERTA','VENCIDA') group by tenant_id, entidade_id;
create or replace view sigov.vw_saneamento_ordens_servico_resumo as select tenant_id, entidade_id, status, count(*) total from sigov.saneamento_ordem_servico where is_deleted=false group by tenant_id, entidade_id, status;
create or replace view sigov.vw_saneamento_leituras_pendentes as select uc.tenant_id, uc.entidade_id, uc.id unidade_consumidora_id, uc.codigo_unidade from sigov.saneamento_unidade_consumidora uc where uc.is_deleted=false and not exists (select 1 from sigov.saneamento_leitura l where l.tenant_id=uc.tenant_id and l.entidade_id=uc.entidade_id and l.unidade_consumidora_id=uc.id and l.competencia=to_char(current_date,'YYYY-MM') and l.is_deleted=false);
create or replace view sigov.vw_saneamento_laboratorio_resumo as select tenant_id, entidade_id, status, count(*) total from sigov.saneamento_laboratorio_amostra where is_deleted=false group by tenant_id, entidade_id, status;
create or replace view sigov.vw_saneamento_rede_resumo as select tenant_id, entidade_id, tipo_rede, situacao, count(*) total, sum(extensao_metros) extensao_metros from sigov.saneamento_rede_trecho where is_deleted=false group by tenant_id, entidade_id, tipo_rede, situacao;

insert into sigov.modulo_saas (codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo)
values ('saneamento','Saneamento','Consumidores, ligações, leituras, faturas, laboratório e rede estrutural.','Operacional',80,'/Saneamento/Dashboard','droplet',true)
on conflict (codigo) do nothing;

insert into sigov.tenant_modulo (tenant_id, modulo_saas_id, habilitado, contratado, ativo)
select t.id, m.id, true, true, true from sigov.tenant t join sigov.modulo_saas m on m.codigo='saneamento'
where t.ambiente = 'Development' or t.slug in ('dev','demo')
on conflict (tenant_id, modulo_saas_id) do nothing;

insert into sigov.permissao (modulo, recurso, acao, chave, descricao, ativo)
values
('saneamento','consumidor','visualizar','saneamento.consumidor.visualizar','Visualizar consumidores de saneamento',true),
('saneamento','consumidor','criar','saneamento.consumidor.criar','Criar consumidores de saneamento',true),
('saneamento','consumidor','editar','saneamento.consumidor.editar','Editar consumidores de saneamento',true),
('saneamento','consumidor','excluir','saneamento.consumidor.excluir','Excluir consumidores de saneamento',true),
('saneamento','consumidor','visualizar_dados_completos','saneamento.consumidor.visualizar_dados_completos','Visualizar dados pessoais completos',true),
('saneamento','ligacao','visualizar','saneamento.ligacao.visualizar','Visualizar ligações',true),
('saneamento','ligacao','criar','saneamento.ligacao.criar','Criar ligações',true),
('saneamento','ligacao','editar','saneamento.ligacao.editar','Editar ligações',true),
('saneamento','ligacao','excluir','saneamento.ligacao.excluir','Excluir ligações',true),
('saneamento','unidade_consumidora','visualizar','saneamento.unidade_consumidora.visualizar','Visualizar unidades consumidoras',true),
('saneamento','unidade_consumidora','criar','saneamento.unidade_consumidora.criar','Criar unidades consumidoras',true),
('saneamento','unidade_consumidora','editar','saneamento.unidade_consumidora.editar','Editar unidades consumidoras',true),
('saneamento','unidade_consumidora','excluir','saneamento.unidade_consumidora.excluir','Excluir unidades consumidoras',true),
('saneamento','hidrometro','visualizar','saneamento.hidrometro.visualizar','Visualizar hidrômetros',true),
('saneamento','hidrometro','criar','saneamento.hidrometro.criar','Criar hidrômetros',true),
('saneamento','hidrometro','editar','saneamento.hidrometro.editar','Editar hidrômetros',true),
('saneamento','hidrometro','excluir','saneamento.hidrometro.excluir','Excluir hidrômetros',true),
('saneamento','leitura','visualizar','saneamento.leitura.visualizar','Visualizar leituras',true),
('saneamento','leitura','criar','saneamento.leitura.criar','Criar leituras',true),
('saneamento','leitura','gerar_fatura','saneamento.leitura.gerar_fatura','Gerar faturas por leitura',true),
('saneamento','fatura','visualizar','saneamento.fatura.visualizar','Visualizar faturas',true),
('saneamento','fatura','criar','saneamento.fatura.criar','Criar faturas',true),
('saneamento','fatura','cancelar','saneamento.fatura.cancelar','Cancelar faturas',true),
('saneamento','fatura','registrar_pagamento_dev','saneamento.fatura.registrar_pagamento_dev','Registrar pagamento manual em Development',true),
('saneamento','arrecadacao','visualizar','saneamento.arrecadacao.visualizar','Visualizar arrecadação',true),
('saneamento','arrecadacao','criar','saneamento.arrecadacao.criar','Criar arrecadação',true),
('saneamento','parcelamento','visualizar','saneamento.parcelamento.visualizar','Visualizar parcelamentos',true),
('saneamento','parcelamento','criar','saneamento.parcelamento.criar','Criar parcelamentos',true),
('saneamento','ordem_servico','visualizar','saneamento.ordem_servico.visualizar','Visualizar ordens',true),
('saneamento','ordem_servico','criar','saneamento.ordem_servico.criar','Criar ordens',true),
('saneamento','ordem_servico','editar','saneamento.ordem_servico.editar','Editar ordens',true),
('saneamento','ordem_servico','executar','saneamento.ordem_servico.executar','Executar ordens',true),
('saneamento','ordem_servico','cancelar','saneamento.ordem_servico.cancelar','Cancelar ordens',true),
('saneamento','laboratorio','visualizar','saneamento.laboratorio.visualizar','Visualizar laboratório',true),
('saneamento','laboratorio','criar','saneamento.laboratorio.criar','Criar amostras',true),
('saneamento','laboratorio','resultado','saneamento.laboratorio.resultado','Registrar resultados',true),
('saneamento','rede','visualizar','saneamento.rede.visualizar','Visualizar rede',true),
('saneamento','rede','criar','saneamento.rede.criar','Criar trechos de rede',true),
('saneamento','dashboard','visualizar','saneamento.dashboard.visualizar','Visualizar dashboard',true),
('saneamento','exportar','executar','saneamento.exportar','Exportar dados',true)
on conflict (modulo, recurso, acao) do nothing;
