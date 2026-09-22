-- Transição de ano letivo: resultados explícitos, progressão parametrizada e rematrícula rastreável.
create table if not exists sigov.educacao_resultado_final (
 id bigint generated always as identity primary key,
 tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
 matricula_id bigint not null references sigov.matricula(id), versao bigint not null default 1,
 resultado varchar(30) not null, status varchar(20) not null default 'RASCUNHO', justificativa text,
 retifica_resultado_id bigint references sigov.educacao_resultado_final(id), publicado_em timestamptz, publicado_por bigint,
 ativo boolean not null default true, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), created_by bigint, updated_at timestamptz, updated_by bigint,
 constraint ck_educacao_resultado_final_resultado check(resultado in ('APROVADO','REPROVADO','CONCLUIDO','TRANSFERIDO','INDEFINIDO')),
 constraint ck_educacao_resultado_final_status check(status in ('RASCUNHO','PUBLICADO','RETIFICADO','CANCELADO')),
 unique(tenant_id,entidade_id,matricula_id,versao)
);
create unique index if not exists ux_educacao_resultado_final_publicado on sigov.educacao_resultado_final(tenant_id,entidade_id,matricula_id) where status='PUBLICADO' and not is_deleted;
create table if not exists sigov.educacao_progressao_config (
 id bigint generated always as identity primary key,
 tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
 serie_origem_id bigint not null references sigov.serie_ano(id), resultado varchar(30) not null,
 serie_destino_id bigint not null references sigov.serie_ano(id),
 esfera_governo varchar(20) not null, tipo_entidade varchar(80) not null, orgao_superior_id bigint,
 unidade_gestora_id bigint, unidade_executora_id bigint, hierarquia_administrativa text,
 abrangencia_territorial varchar(80), uf char(2), municipio varchar(150), regiao_jurisdicao varchar(150),
 ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint, updated_at timestamptz, updated_by bigint,
 constraint ck_educacao_progressao_esfera check(esfera_governo in ('municipal','estadual','federal')),
 constraint ck_educacao_progressao_resultado check(resultado in ('APROVADO','REPROVADO','CONCLUIDO')),
 unique(tenant_id,entidade_id,serie_origem_id,resultado)
);
alter table sigov.matricula add column if not exists origem_matricula_id bigint references sigov.matricula(id);
create unique index if not exists ux_matricula_rematricula_origem on sigov.matricula(tenant_id,entidade_id,origem_matricula_id,ano_letivo_id,turma_id) where origem_matricula_id is not null and status<>'CANCELADA' and not is_deleted;
create table if not exists sigov.educacao_rematricula_operacao (
 id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
 escola_id bigint not null references sigov.escola(id), ano_letivo_origem_id bigint not null references sigov.ano_letivo(id), ano_letivo_destino_id bigint not null references sigov.ano_letivo(id),
 chave_operacao varchar(120) not null, request_hash char(64) not null, situacao varchar(20) not null, quantidade_itens int not null, concluidos int not null default 0, falhas int not null default 0,
 responsavel_id bigint not null, created_at timestamptz not null default now(), created_by bigint, updated_at timestamptz,
 constraint ck_rematricula_anos_distintos check(ano_letivo_origem_id<>ano_letivo_destino_id), constraint ck_rematricula_operacao_situacao check(situacao in ('PROCESSANDO','CONCLUIDA','PARCIAL','FALHA')),
 unique(tenant_id,entidade_id,chave_operacao)
);
create table if not exists sigov.educacao_rematricula_item (
 id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id), operacao_id bigint not null references sigov.educacao_rematricula_operacao(id),
 matricula_origem_id bigint not null references sigov.matricula(id), turma_destino_id bigint not null references sigov.turma(id), matricula_destino_id bigint references sigov.matricula(id),
 token_conferencia varchar(64) not null, situacao varchar(20) not null, mensagem text not null, created_at timestamptz not null default now(), created_by bigint,
 constraint ck_rematricula_item_situacao check(situacao in ('CONCLUIDO','FALHA')), unique(operacao_id,matricula_origem_id)
);
create index if not exists ix_rematricula_operacao_consulta on sigov.educacao_rematricula_operacao(tenant_id,entidade_id,created_at desc);

create or replace function sigov.fn_educacao_confirmar_rematricula(p_tenant bigint,p_entidade bigint,p_exercicio bigint,p_operacao bigint,p_origem bigint,p_destino bigint,p_token text,p_usuario bigint)
returns bigint language plpgsql security invoker set search_path=sigov,pg_temp as $$
declare v_m sigov.matricula%rowtype; v_t sigov.turma%rowtype; v_r sigov.educacao_resultado_final%rowtype; v_op sigov.educacao_rematricula_operacao%rowtype; v_existente bigint; v_token text; v_id bigint; v_numero text;
begin
 select * into v_op from sigov.educacao_rematricula_operacao where id=p_operacao and tenant_id=p_tenant and entidade_id=p_entidade for update;
 if not found then raise exception 'Operação fora do contexto autorizado'; end if;
 select * into v_m from sigov.matricula where id=p_origem and tenant_id=p_tenant and entidade_id=p_entidade and escola_id=v_op.escola_id and ano_letivo_id=v_op.ano_letivo_origem_id and status in ('ATIVA','CONFIRMADA','CONCLUIDA') and not is_deleted for update;
 if not found then raise exception 'Matrícula de origem mudou ou não está vigente'; end if;
 select * into v_r from sigov.educacao_resultado_final where tenant_id=p_tenant and entidade_id=p_entidade and matricula_id=p_origem and status='PUBLICADO' and not is_deleted order by versao desc limit 1 for update;
 if not found then raise exception 'Resultado final publicado não encontrado'; end if;
 if v_r.resultado not in ('APROVADO','REPROVADO','CONCLUIDO') then raise exception 'Resultado final exige análise autorizada'; end if;
 select * into v_t from sigov.turma where id=p_destino and tenant_id=p_tenant and entidade_id=p_entidade and escola_id=v_op.escola_id and ano_letivo_id=v_op.ano_letivo_destino_id and status in ('ABERTA','PLANEJADA') and ativo and not is_deleted for update;
 if not found then raise exception 'Oferta de destino foi desativada ou alterada'; end if;
 select id into v_existente from sigov.matricula where tenant_id=p_tenant and entidade_id=p_entidade and aluno_id=v_m.aluno_id and origem_matricula_id=p_origem and ano_letivo_id=v_op.ano_letivo_destino_id and status<>'CANCELADA' and not is_deleted limit 1;
 if v_existente is not null then raise exception 'Aluno já rematriculado por outra operação'; end if;
 v_token=md5(concat_ws('|',v_m.id,v_r.id,v_r.versao,v_r.resultado,v_r.status,v_t.id,v_t.vagas_ocupadas,v_t.status,coalesce(v_t.updated_at,v_t.created_at),0));
 if v_token<>p_token then raise exception 'Dados alterados após a prévia; atualize a simulação'; end if;
 if v_t.vagas_ocupadas>=v_t.capacidade then raise exception 'Última vaga ocupada por outra operação'; end if;
 v_numero='REM-'||(select ano from sigov.ano_letivo where id=v_t.ano_letivo_id)||'-'||lpad(nextval('sigov.educacao_numero_seq')::text,8,'0');
 insert into sigov.matricula(tenant_id,entidade_id,exercicio_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,origem,origem_matricula_id,observacao,created_by)
 values(p_tenant,p_entidade,p_exercicio,v_m.aluno_id,v_t.escola_id,v_t.ano_letivo_id,v_t.id,v_numero,current_date,'ATIVA','REMATRICULA',v_m.id,'Criada pela transição de ano letivo; dados acadêmicos não copiados.',p_usuario) returning id into v_id;
 update sigov.turma set vagas_ocupadas=vagas_ocupadas+1,updated_at=now(),updated_by=p_usuario where id=v_t.id;
 perform 1 from sigov.matricula where id=v_id and tenant_id=p_tenant and entidade_id=p_entidade;
 if not found then raise exception 'Falha ao reler matrícula persistida'; end if;
 return v_id;
end $$;

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo,is_deleted) values
 ('educacao','rematricula','visualizar','educacao.rematricula.visualizar','Consultar transição e histórico de ano letivo',true,false),
 ('educacao','rematricula','simular','educacao.rematricula.simular','Simular rematrícula sem consumir vaga',true,false),
 ('educacao','rematricula','confirmar','educacao.rematricula.confirmar','Confirmar rematrícula individual ou em lote',true,false),
 ('educacao','rematricula','cancelar','educacao.rematricula.cancelar','Cancelar rematrícula após verificar dependências',true,false)
on conflict(modulo,chave) do update set descricao=excluded.descricao,ativo=true,is_deleted=false;
