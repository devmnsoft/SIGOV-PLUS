-- RC-EVO Ciclo 2 Sprint 4 (transporte escolar: da rota a viagem) - incrementos BR-4.1 a BR-4.8.
-- Idempotente (regra 6); multi-esfera (regras 21-24): tudo por tenant x entidade, sem municipio hardcoded.
-- As tabelas base (veiculo, motorista, viagem, monitor, ocorrencia, custo, frequencia) ja existem no
-- schema generico; esta migration acrescenta colunas dedicadas onde ha consulta/indice, backstops de
-- unicidade parcial (sobrepocao, presenca, custo realizado), o catalogo configuravel de compatibilidade
-- categoria de habilitacao x classe de veiculo e os parametros da esfera no catalogo do modulo EDUCACAO.

-- 1) Veiculo (BR-4.1): placa, capacidade, situacao documental e inspecao com validade parametrizavel
alter table sigov.educacao_transporte_veiculo add column if not exists placa varchar(20);
alter table sigov.educacao_transporte_veiculo add column if not exists capacidade integer;
alter table sigov.educacao_transporte_veiculo add column if not exists situacao_documental varchar(40) not null default 'REGULAR';
alter table sigov.educacao_transporte_veiculo add column if not exists inspecao_validade date;
-- um cadastro vigente por placa por tenant (backstop transacional do comando tipado)
create unique index if not exists ux_transporte_veiculo_tenant_placa on sigov.educacao_transporte_veiculo(tenant_id, placa) where is_deleted=false and ativo=true and placa is not null;

-- 2) Motorista (BR-4.2): categoria da habilitacao e validade da licenca
alter table sigov.educacao_transporte_motorista add column if not exists habilitacao_categoria varchar(5);
alter table sigov.educacao_transporte_motorista add column if not exists habilitacao_validade date;

-- 3) Viagem (BR-4.3/BR-4.7): hora inicio/fim, km, componentes da estimativa de custo (BR-4.6),
-- versao da substituicao com cronologia append-only, monitor(es) em jsonb e bandeira de presenca fechada
alter table sigov.educacao_transporte_viagem add column if not exists monitor_id bigint;
alter table sigov.educacao_transporte_viagem add column if not exists hora_inicio timestamptz;
alter table sigov.educacao_transporte_viagem add column if not exists hora_fim timestamptz;
alter table sigov.educacao_transporte_viagem add column if not exists km_estimado numeric(12,2);
alter table sigov.educacao_transporte_viagem add column if not exists km_percorridos numeric(12,2);
alter table sigov.educacao_transporte_viagem add column if not exists tarifa_km numeric(14,4);
alter table sigov.educacao_transporte_viagem add column if not exists combustivel_estimado numeric(14,2);
alter table sigov.educacao_transporte_viagem add column if not exists pedagio_previsto numeric(14,2);
alter table sigov.educacao_transporte_viagem add column if not exists versao integer not null default 1;
alter table sigov.educacao_transporte_viagem add column if not exists presenca_fechada boolean not null default false;
-- backstops de substituicao SEM sobreposicao (BR-4.3): uma viagem vigente (PROGRAMADA/EM_ANDAMENTO)
-- por veiculo x data e por motorista x data; CONCLUIDA/CANCELADA/SUBSTITUIDA liberam a posicao
create unique index if not exists ux_transporte_viagem_tenant_veiculo_data on sigov.educacao_transporte_viagem(tenant_id, coalesce(veiculo_id,(0)::bigint), data_inicio) where is_deleted=false and ativo=true and status in ('PROGRAMADA','EM_ANDAMENTO');
create unique index if not exists ux_transporte_viagem_tenant_motorista_data on sigov.educacao_transporte_viagem(tenant_id, coalesce(motorista_id,(0)::bigint), data_inicio) where is_deleted=false and ativo=true and status in ('PROGRAMADA','EM_ANDAMENTO');

-- 4) Presenca por viagem (BR-4.4): dominio proprio na educacao_transporte_frequencia com
-- tipo='PRESENCA_TRANSPORTE' (NUNCA altera a frequencia escolar - apenas correlaciona).
-- Dedupe legado preservando min(id) antes do backstop unico (mesmo padrao do dedupe do C2S1/C2S3).
delete from sigov.educacao_transporte_frequencia d using sigov.educacao_transporte_frequencia k
 where k.tenant_id=d.tenant_id
   and k.referencia_id is not distinct from d.referencia_id
   and k.aluno_id is not distinct from d.aluno_id
   and k.tipo='PRESENCA_TRANSPORTE' and d.tipo='PRESENCA_TRANSPORTE'
   and d.ativo=true and d.is_deleted=false and k.ativo=true and k.is_deleted=false
   and k.status <> 'ESTORNADO' and d.status <> 'ESTORNADO'
   and k.id < d.id;
create unique index if not exists ux_transporte_presenca_tenant_viagem_aluno on sigov.educacao_transporte_frequencia(tenant_id, coalesce(referencia_id,(0)::bigint), coalesce(aluno_id,(0)::bigint)) where is_deleted=false and ativo=true and tipo='PRESENCA_TRANSPORTE' and status <> 'ESTORNADO';

-- 5) Ocorrencia de viagem (BR-4.5): gravidade canonica, data/hora do fato, desfecho (relatorio)
alter table sigov.educacao_transporte_ocorrencia add column if not exists gravidade varchar(20);
alter table sigov.educacao_transporte_ocorrencia add column if not exists data_hora timestamptz not null default now();
alter table sigov.educacao_transporte_ocorrencia add column if not exists desfecho_relatorio text;
alter table sigov.educacao_transporte_ocorrencia add column if not exists desfecho_em timestamptz;
-- ocorrencia grave (ALTA) por viagem: unica vigente (outros tipos nao sao unicos por viagem)
create unique index if not exists ux_transporte_ocorrencia_tenant_viagem_grave on sigov.educacao_transporte_ocorrencia(tenant_id, coalesce(referencia_id,(0)::bigint)) where is_deleted=false and ativo=true and tipo='ACIDENTE' and gravidade='ALTA' and desfecho_em is null;

-- 6) Custo realizado (BR-4.6): um custo REALIZADO por viagem (comando proprio, sem edicao manual);
-- o conjunto ESTIMADO permanece intocado e os dois sao comparaveis no relatorio
delete from sigov.educacao_transporte_custo d using sigov.educacao_transporte_custo k
 where k.tenant_id=d.tenant_id
   and k.referencia_id is not distinct from d.referencia_id
   and k.tipo='REALIZADO' and d.tipo='REALIZADO'
   and d.ativo=true and d.is_deleted=false and k.ativo=true and k.is_deleted=false
   and k.id < d.id;
create unique index if not exists ux_transporte_custo_tenant_viagem_realizado on sigov.educacao_transporte_custo(tenant_id, coalesce(referencia_id,(0)::bigint)) where is_deleted=false and ativo=true and tipo='REALIZADO';

-- 7) Catalogo configuravel categoria de habilitacao -> classe de veiculo (BR-4.2; CONTRAN como
-- referencia federal valida para todas as esferas; o banco e a autoridade - regra 11)
create table if not exists sigov.transporte_categoria_compatibilidade(id bigint generated by default as identity primary key,categoria_habilitacao varchar(5) not null,classe_veiculo varchar(40) not null,descricao varchar(240),ativo boolean not null default true,is_deleted boolean not null default false,created_at timestamptz not null default now());
create unique index if not exists ux_transporte_categoria_compat on sigov.transporte_categoria_compatibilidade(categoria_habilitacao, classe_veiculo) where is_deleted=false;
insert into sigov.transporte_categoria_compatibilidade(categoria_habilitacao,classe_veiculo,descricao) values
 ('C','CARRO','Categoria C: veiculos de transporte leve'),
 ('D','VAN','Categoria D: van/micro-veiculo escolar'),
 ('D','MICRO_ONIBUS','Categoria D: micro-onibus escolar'),
 ('D','CAMIONETE','Categoria D: camionete'),
 ('E','ONIBUS','Categoria E: onibus escolar pesado')
on conflict do nothing;

-- 8) Parametros de transporte (BR-4.1/BR-4.3/BR-4.5): catalogo global do modulo EDUCACAO; o tenant
-- pode sobrescrever via parametro_modulo_valor - banco como autoridade (regra 11)
insert into sigov.parametro_modulo(modulo,codigo,nome,descricao,tipo,valor_padrao,sensivel,ordem,ativo)
values
 ('EDUCACAO','TRANSPORTE_INSPECCAO_VALIDADE_MESES','Meses de validade de inspecao/revisao do veiculo','BR-4.1: janela de validade da inspecao do veiculo para viagem','INTEGER','6',false,170,true),
 ('EDUCACAO','TRANSPORTE_INSPECCAO_MODO','Modo de tratamento de inspecao vencida','BR-4.1: ALERTA prossegue a alocacao; BLOQUEIO impede veiculo em viagem','TEXT','"BLOQUEIO"',false,180,true),
 ('EDUCACAO','TRANSPORTE_MONITOR_OBRIGATORIO','Exigir monitor em toda viagem','BR-4.3: quando exigido pela esfera/jurisdicao do tenant, viagem sem monitor e rejeitada','TEXT','"FALSE"',false,190,true),
 ('EDUCACAO','TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS','Carona de horas apos ocorrencia grave desfechada','BR-4.5: nova viagem do mesmo veiculo so apos N horas de ocorrencia ALTA desfechada (relatorio)','INTEGER','24',false,200,true)
on conflict do nothing;

-- C2S4-POSTCOND: falha explicita se faltar qualquer artefato do incremento (regra 13)
do $$
begin
  if not exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='educacao_transporte_veiculo' and column_name='situacao_documental') then
    raise exception 'C2S4-POSTCOND: coluna sigov.educacao_transporte_veiculo.situacao_documental ausente';
  end if;
  if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_transporte_viagem_tenant_veiculo_data') then
    raise exception 'C2S4-POSTCOND: indice ux_transporte_viagem_tenant_veiculo_data ausente';
  end if;
  if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_transporte_viagem_tenant_motorista_data') then
    raise exception 'C2S4-POSTCOND: indice ux_transporte_viagem_tenant_motorista_data ausente';
  end if;
  if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_transporte_presenca_tenant_viagem_aluno') then
    raise exception 'C2S4-POSTCOND: indice ux_transporte_presenca_tenant_viagem_aluno ausente';
  end if;
  if not exists(select 1 from pg_indexes where schemaname='sigov' and indexname='ux_transporte_custo_tenant_viagem_realizado') then
    raise exception 'C2S4-POSTCOND: indice ux_transporte_custo_tenant_viagem_realizado ausente';
  end if;
  if (select count(1) from sigov.transporte_categoria_compatibilidade where is_deleted=false)=0 then
    raise exception 'C2S4-POSTCOND: catalogo transporte_categoria_compatibilidade vazio';
  end if;
  if (select count(1) from sigov.parametro_modulo where modulo='EDUCACAO' and codigo in ('TRANSPORTE_INSPECCAO_VALIDADE_MESES','TRANSPORTE_INSPECCAO_MODO','TRANSPORTE_MONITOR_OBRIGATORIO','TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS') and ativo and not is_deleted)<>4 then
    raise exception 'C2S4-POSTCOND: parametros de transporte ausentes no catalogo';
  end if;
end
$$;
