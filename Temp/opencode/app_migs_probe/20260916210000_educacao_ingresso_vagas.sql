-- Jornada administrativa de ingresso e vagas, sobre os cadastros canônicos da Educação.
alter table sigov.pre_matricula_inscricao
  add column if not exists turno varchar(40),
  add column if not exists versao bigint not null default 1,
  add column if not exists responsavel_analise_id bigint,
  add column if not exists modalidade varchar(80);

alter table sigov.matricula alter column turma_id drop not null;
alter table sigov.matricula add column if not exists pre_matricula_id bigint references sigov.pre_matricula_inscricao(id);
alter table sigov.matricula add column if not exists data_enturmacao date;

create table if not exists sigov.educacao_oferta_vaga (
 id bigint generated always as identity primary key,
 tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
 pre_matricula_id bigint not null references sigov.pre_matricula_inscricao(id), escola_id bigint not null references sigov.escola(id),
 ano_letivo_id bigint not null references sigov.ano_letivo(id), serie_ano_id bigint not null references sigov.serie_ano(id),
 turno varchar(40) not null, inicio date not null, valida_ate timestamptz, status varchar(20) not null,
 responsavel_id bigint not null, motivo_decisao text, decidida_em timestamptz,
 ativo boolean not null default true, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), created_by bigint, updated_at timestamptz, updated_by bigint,
 constraint ck_educacao_oferta_status check(status in ('OFERTADA','ACEITA','RECUSADA','EXPIRADA','CONSUMIDA','CANCELADA')),
 constraint ck_educacao_oferta_validade check(valida_ate is null or valida_ate > created_at)
);
create unique index if not exists ux_educacao_oferta_valida_solicitacao on sigov.educacao_oferta_vaga(tenant_id,entidade_id,pre_matricula_id) where status in ('OFERTADA','ACEITA') and not is_deleted;
create index if not exists ix_educacao_oferta_capacidade on sigov.educacao_oferta_vaga(tenant_id,entidade_id,escola_id,ano_letivo_id,serie_ano_id,turno,status,valida_ate) where not is_deleted;
create unique index if not exists ux_matricula_origem_prematricula on sigov.matricula(tenant_id,entidade_id,pre_matricula_id) where pre_matricula_id is not null and not is_deleted;
create index if not exists ix_prematricula_analise on sigov.pre_matricula_inscricao(tenant_id,entidade_id,ano_letivo,escola_preferencial_id,etapa_ensino,turno,status,responsavel_analise_id,id) where not is_deleted;

create table if not exists sigov.educacao_prematricula_pendencia (
 id bigint generated always as identity primary key, tenant_id bigint not null references sigov.tenant(id), entidade_id bigint not null references sigov.entidade(id),
 pre_matricula_id bigint not null references sigov.pre_matricula_inscricao(id), requisito varchar(200) not null,
 situacao varchar(20) not null default 'PENDENTE', observacao text not null, responsavel_analise_id bigint not null,
 analisada_em timestamptz not null default now(), created_at timestamptz not null default now(), created_by bigint,
 constraint ck_educacao_pendencia_situacao check(situacao in ('PENDENTE','RESOLVIDA','CANCELADA'))
);


create sequence if not exists sigov.educacao_numero_seq;

create or replace function sigov.fn_educacao_converter_oferta(p_tenant bigint,p_entidade bigint,p_exercicio bigint,p_prematricula bigint,p_oferta bigint,p_turma bigint,p_numero text,p_data date,p_usuario bigint)
returns bigint language plpgsql security invoker set search_path=sigov,pg_temp as $$
declare v_oferta sigov.educacao_oferta_vaga%rowtype; v_pre sigov.pre_matricula_inscricao%rowtype; v_aluno bigint; v_id bigint; v_numero text;
begin
 select * into v_oferta from sigov.educacao_oferta_vaga where id=p_oferta and tenant_id=p_tenant and entidade_id=p_entidade for update;
 if not found or v_oferta.pre_matricula_id<>p_prematricula or v_oferta.status<>'ACEITA' or (v_oferta.valida_ate is not null and v_oferta.valida_ate<=now()) then raise exception 'Oferta não aceita, expirada ou incompatível'; end if;
 select * into v_pre from sigov.pre_matricula_inscricao where id=p_prematricula and tenant_id=p_tenant and entidade_id=p_entidade for update;
 select id into v_aluno from sigov.aluno where tenant_id=p_tenant and entidade_id=p_entidade and pessoa_id=v_pre.aluno_pessoa_id and situacao='ATIVO' and not is_deleted;
 if v_aluno is null then raise exception 'Aluno canônico ativo não encontrado para a pessoa da solicitação'; end if;
 if exists(select 1 from sigov.educacao_prematricula_pendencia where tenant_id=p_tenant and entidade_id=p_entidade and pre_matricula_id=p_prematricula and situacao='PENDENTE') then raise exception 'Solicitação possui pendência impeditiva'; end if;
 if p_turma is not null then
   perform 1 from sigov.turma where id=p_turma and tenant_id=p_tenant and entidade_id=p_entidade and escola_id=v_oferta.escola_id and ano_letivo_id=v_oferta.ano_letivo_id and serie_ano_id=v_oferta.serie_ano_id and turno=v_oferta.turno and status='ABERTA' and vagas_ocupadas<capacidade and not is_deleted for update;
   if not found then raise exception 'Turma incompatível, encerrada ou sem capacidade'; end if;
   update sigov.turma set vagas_ocupadas=vagas_ocupadas+1,updated_by=p_usuario where id=p_turma;
 end if;
 v_numero=coalesce(nullif(trim(p_numero),''),'MAT-'||extract(year from now())::int||'-'||lpad(nextval('sigov.educacao_numero_seq')::text,8,'0'));
 insert into sigov.matricula(tenant_id,entidade_id,exercicio_id,aluno_id,escola_id,ano_letivo_id,turma_id,numero_matricula,data_matricula,status,origem,pre_matricula_id,created_by)
 values(p_tenant,p_entidade,p_exercicio,v_aluno,v_oferta.escola_id,v_oferta.ano_letivo_id,p_turma,v_numero,coalesce(p_data,current_date),'ATIVA','PRE_MATRICULA',p_prematricula,p_usuario) returning id into v_id;
 update sigov.educacao_oferta_vaga set status='CONSUMIDA',decidida_em=now(),updated_by=p_usuario where id=p_oferta;
 update sigov.pre_matricula_inscricao set status='CONVERTIDA_MATRICULA',versao=versao+1,updated_by=p_usuario where id=p_prematricula;
 return v_id;
end $$;

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo)
values ('educacao','pre_matricula','editar','educacao.pre_matricula.editar','Editar e transicionar pré-matrícula',true),
       ('educacao','pre_matricula','deferir','educacao.pre_matricula.deferir','Analisar e ofertar vaga',true),
       ('educacao','matricula','enturmar','educacao.matricula.enturmar','Enturmar matrícula vigente',true)
on conflict(chave) do update set modulo=excluded.modulo,recurso=excluded.recurso,acao=excluded.acao,descricao=excluded.descricao,ativo=true;
