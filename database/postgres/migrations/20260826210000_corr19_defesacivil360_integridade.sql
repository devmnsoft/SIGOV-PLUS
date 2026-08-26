-- CORR19 DefesaCivil360: integridade operacional, isolamento contextual e fechamento fail-closed.
create schema if not exists sigov;

do $corr19$
begin
 if to_regclass('sigov.defesa_civil_area_risco') is null then raise exception 'CORR19 requer a migration EXP19 aplicada'; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_cenario_status') then alter table sigov.defesa_civil_cenario_risco add constraint ck_corr19_cenario_status check(status in('ATIVO','INATIVO')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_cenario_probabilidade') then alter table sigov.defesa_civil_cenario_risco add constraint ck_corr19_cenario_probabilidade check(probabilidade in('BAIXA','MEDIA','ALTA','MUITO_ALTA')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_equipe_status') then alter table sigov.defesa_civil_equipe add constraint ck_corr19_equipe_status check(status in('ATIVA','INATIVA')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_abrigo_status') then alter table sigov.defesa_civil_abrigo add constraint ck_corr19_abrigo_status check(status in('ATIVO','INATIVO')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_ocorrencia_status') then alter table sigov.defesa_civil_ocorrencia add constraint ck_corr19_ocorrencia_status check(status in('ABERTA','EM_ATENDIMENTO','ENCERRADA','CANCELADA')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_resposta_status') then alter table sigov.defesa_civil_ocorrencia_resposta add constraint ck_corr19_resposta_status check(status in('EM_ANDAMENTO','CONCLUIDA','CANCELADA')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_alerta_status') then alter table sigov.defesa_civil_alerta add constraint ck_corr19_alerta_status check(status in('RASCUNHO','APROVADO','PUBLICADO','EXPIRADO','CANCELADO')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_alerta_publicacao') then alter table sigov.defesa_civil_alerta add constraint ck_corr19_alerta_publicacao check(status<>'PUBLICADO' or (aprovado_por is not null and publicado_em is not null)) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_plano_publicacao') then alter table sigov.defesa_civil_plano_contingencia add constraint ck_corr19_plano_publicacao check(status not in('APROVADO','PUBLICADO') or (aprovado_por is not null and aprovado_em is not null and vigencia_fim>=current_date)) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_ocupacao_status') then alter table sigov.defesa_civil_abrigo_ocupacao add constraint ck_corr19_ocupacao_status check(status in('ABRIGADA','ENCERRADA')) not valid; end if;
 if not exists(select 1 from pg_constraint where conname='ck_corr19_doacao_status') then alter table sigov.defesa_civil_doacao add constraint ck_corr19_doacao_status check(status in('RECEBIDA','DESTINADA','PRESTADA','CANCELADA')) not valid; end if;
end$corr19$;

create or replace function sigov.fn_corr19_validar_contexto_fk() returns trigger language plpgsql as $fn$
declare ref_tenant bigint; ref_entity bigint; ref_exercicio bigint;
begin
 if tg_argv[0]='area' then select tenant_id,entity_id,exercicio_id into ref_tenant,ref_entity,ref_exercicio from sigov.defesa_civil_area_risco where id=new.area_risco_id and deleted_at is null;
 elsif tg_argv[0]='cenario' then select tenant_id,entity_id,exercicio_id into ref_tenant,ref_entity,ref_exercicio from sigov.defesa_civil_cenario_risco where id=new.cenario_risco_id and deleted_at is null;
 elsif tg_argv[0]='plano' then select tenant_id,entity_id,exercicio_id into ref_tenant,ref_entity,ref_exercicio from sigov.defesa_civil_plano_contingencia where id=new.plano_id and deleted_at is null;
 elsif tg_argv[0]='ocorrencia' then select tenant_id,entity_id,exercicio_id into ref_tenant,ref_entity,ref_exercicio from sigov.defesa_civil_ocorrencia where id=new.ocorrencia_id and deleted_at is null;
 elsif tg_argv[0]='abrigo' then select tenant_id,entity_id,exercicio_id into ref_tenant,ref_entity,ref_exercicio from sigov.defesa_civil_abrigo where id=new.abrigo_id and deleted_at is null;
 end if;
 if ref_tenant is null or (ref_tenant,ref_entity,ref_exercicio) is distinct from (new.tenant_id,new.entity_id,new.exercicio_id) then raise exception 'Vínculo fora do contexto tenant/entidade/exercício'; end if;
 return new;
end$fn$;

do $triggers$ declare item text; parts text[]; begin
 foreach item in array array['tr_corr19_cenario_area|defesa_civil_cenario_risco|area','tr_corr19_plano_cenario|defesa_civil_plano_contingencia|cenario','tr_corr19_rota_plano|defesa_civil_rota_evacuacao|plano','tr_corr19_resposta_ocorrencia|defesa_civil_ocorrencia_resposta|ocorrencia','tr_corr19_estoque_abrigo|defesa_civil_estoque|abrigo','tr_corr19_ocupacao_abrigo|defesa_civil_abrigo_ocupacao|abrigo'] loop
  parts:=string_to_array(item,'|'); execute format('drop trigger if exists %I on sigov.%I',parts[1],parts[2]); execute format('create trigger %I before insert or update on sigov.%I for each row execute function sigov.fn_corr19_validar_contexto_fk(%L)',parts[1],parts[2],parts[3]);
 end loop;
end$triggers$;

create or replace function sigov.fn_corr19_validar_operacao() returns trigger language plpgsql as $fn$
declare capacidade_abrigo integer; ocupacao integer;
begin
 if tg_table_name='defesa_civil_ocorrencia' and new.status='ENCERRADA' and (old.status is distinct from new.status) then
  if new.encerrada_em is null or length(btrim(new.descricao))<20 then raise exception 'Encerramento exige data e resultado descritivo mínimo'; end if;
  if not exists(select 1 from sigov.defesa_civil_evidencia_vinculo e where e.tenant_id=new.tenant_id and e.entity_id=new.entity_id and e.exercicio_id=new.exercicio_id and e.contexto_tipo='OCORRENCIA' and e.contexto_id=new.id and e.status='ATIVA' and e.deleted_at is null) then raise exception 'Encerramento exige evidência transversal ativa'; end if;
 end if;
 if tg_table_name='defesa_civil_ocorrencia_resposta' and new.status<>'CANCELADA' and exists(select 1 from sigov.defesa_civil_ocorrencia_resposta r where r.id<>coalesce(new.id,0) and r.tenant_id=new.tenant_id and r.entity_id=new.entity_id and r.exercicio_id=new.exercicio_id and r.ocorrencia_id=new.ocorrencia_id and r.status<>'CANCELADA' and r.deleted_at is null and (new.equipe_id is not null and r.equipe_id=new.equipe_id or new.recurso_id is not null and r.recurso_id=new.recurso_id) and tstzrange(r.inicio_em,coalesce(r.fim_em,'infinity')) && tstzrange(new.inicio_em,coalesce(new.fim_em,'infinity'))) then raise exception 'Equipe ou recurso já alocado no período'; end if;
 if tg_table_name='defesa_civil_abrigo_ocupacao' and new.status='ABRIGADA' and new.saida_em is null then
  select capacidade into capacidade_abrigo from sigov.defesa_civil_abrigo where id=new.abrigo_id and tenant_id=new.tenant_id and entity_id=new.entity_id and deleted_at is null for update;
  select count(*) into ocupacao from sigov.defesa_civil_abrigo_ocupacao where abrigo_id=new.abrigo_id and tenant_id=new.tenant_id and entity_id=new.entity_id and status='ABRIGADA' and saida_em is null and deleted_at is null and id<>coalesce(new.id,0);
  if ocupacao>=capacidade_abrigo then raise exception 'Capacidade do abrigo esgotada; excedente depende de fluxo autorizado e auditado'; end if;
 end if;
 return new;
end$fn$;

drop trigger if exists tr_corr19_encerrar_ocorrencia on sigov.defesa_civil_ocorrencia;
create trigger tr_corr19_encerrar_ocorrencia before update on sigov.defesa_civil_ocorrencia for each row execute function sigov.fn_corr19_validar_operacao();
drop trigger if exists tr_corr19_alocacao_resposta on sigov.defesa_civil_ocorrencia_resposta;
create trigger tr_corr19_alocacao_resposta before insert or update on sigov.defesa_civil_ocorrencia_resposta for each row execute function sigov.fn_corr19_validar_operacao();
drop trigger if exists tr_corr19_capacidade_abrigo on sigov.defesa_civil_abrigo_ocupacao;
create trigger tr_corr19_capacidade_abrigo before insert or update on sigov.defesa_civil_abrigo_ocupacao for each row execute function sigov.fn_corr19_validar_operacao();

create unique index if not exists ux_corr19_codigo_area on sigov.defesa_civil_area_risco(tenant_id,entity_id,exercicio_id,codigo) where deleted_at is null;
create unique index if not exists ux_corr19_codigo_ocorrencia on sigov.defesa_civil_ocorrencia(tenant_id,entity_id,exercicio_id,codigo) where deleted_at is null;
create unique index if not exists ux_corr19_codigo_plano on sigov.defesa_civil_plano_contingencia(tenant_id,entity_id,exercicio_id,codigo,versao) where deleted_at is null;
create unique index if not exists ux_corr19_ocupacao_pessoa on sigov.defesa_civil_abrigo_ocupacao(tenant_id,entity_id,exercicio_id,pessoa_id) where deleted_at is null and status='ABRIGADA' and saida_em is null;
