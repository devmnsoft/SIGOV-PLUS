-- PROBE: replicar EnviarAsync(Rq2) passo a passo dentro de uma transacao que sera rolada
\t on
begin;
select '=== 1 advisory lock';
select pg_advisory_xact_lock(hashtextextended('b0000001-0000-4000-8000-000000000001|REQUISICAO|d0000001-0000-4000-8000-000000000002|ENVIO',0));
select '=== 2 status/version for update';
select status,version from sigov.compras_empresarial_requisicao where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and id='d0000001-0000-4000-8000-000000000002'::uuid and not is_deleted for update;
select '=== 3 itens/total';
select count(*) as itens from sigov.compras_empresarial_requisicao_item i where i.tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and i.requisicao_id='d0000001-0000-4000-8000-000000000002'::uuid and not i.is_deleted;
select coalesce(round(sum(i.quantidade*i.valor_estimado),2),0) as total from sigov.compras_empresarial_requisicao_item i where i.tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and i.requisicao_id='d0000001-0000-4000-8000-000000000002'::uuid and not i.is_deleted;
select '=== 4 idempotencia lookup';
select request_hash from sigov.compras_empresarial_idempotencia where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and operacao='REQUISICAO_ENVIAR' and chave='jornada-rq2-enviar-k1';
select '=== 5 update valor_estimado';
update sigov.compras_empresarial_requisicao set valor_estimado=40000,updated_at=now(),updated_by='101',correlation_id='f1000000-0000-4000-8000-0000000000d4' where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and id='d0000001-0000-4000-8000-000000000002'::uuid;
select '=== 6 resolver nucleo (count vinculos)';
select count(*) as vinculos from sigov.enterprise_tenant_mapping where enterprise_tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and ativo;
select '=== 7 politica ativa';
select id,nome from sigov.compras_empresarial_aprovacao_politica where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and ativo and not is_deleted order by id desc limit 1;
select '=== 8 proximo ciclo';
select coalesce(max(ciclo),0)+1 as ciclo from sigov.compras_empresarial_aprovacao where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and requisicao_id='d0000001-0000-4000-8000-000000000002'::uuid;
select '=== 9 insere etapa bloqueada (sem politica)';
insert into sigov.compras_empresarial_aprovacao(id,tenant_id,requisicao_id,nivel,aprovador_id,limite,status,regra_snapshot,ciclo,created_by,updated_by,correlation_id)
values(gen_random_uuid(),'b0000001-0000-4000-8000-000000000001'::uuid,'d0000001-0000-4000-8000-000000000002'::uuid,1,null,0,'PENDENTE',jsonb_build_object('total_requisicao',40000,'alcada_etapa',0,'etapa',1,'ciclo',1,'politica_id',null,'politica_nome',null,'itens',(select coalesce(jsonb_agg(jsonb_build_object('ordem',i.ordem,'quantidade',i.quantidade,'valor_estimado',i.valor_estimado) order by i.ordem),'[]'::jsonb) from sigov.compras_empresarial_requisicao_item i where i.tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and i.requisicao_id='d0000001-0000-4000-8000-000000000002'::uuid and not i.is_deleted),1,'101','101','f1000000-0000-4000-8000-0000000000d4'));
select '=== 10 registrar pendencia';
insert into sigov.pendencia_operacional(tenant_id,modulo,recurso,tipo,entidade,entidade_id,gravidade,titulo,descricao,rota_acao,status)
select m.core_tenant_id,'COMPRAS_EMPRESARIAIS','APROVACAO','APROVACAO_SEM_POLITICA','compras_empresarial_requisicao','d0000001-0000-4000-8000-000000000002'::text,'ALTA','Politica de aprovacao ausente','Configure a politica.','/ComprasEmpresariais/Aprovacoes','ABERTA'
from sigov.enterprise_tenant_mapping m where m.enterprise_tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and m.ativo
on conflict(tenant_id,modulo,tipo,entidade,entidade_id) where status in('ABERTA','EM_TRATAMENTO') do update set titulo=excluded.titulo,descricao=excluded.descricao,rota_acao=excluded.rota_acao;
select '=== 11 update status PENDENTE_APROVACAO';
update sigov.compras_empresarial_requisicao set status='PENDENTE_APROVACAO',valor_estimado=40000,version=version+1,updated_at=now(),updated_by='101',correlation_id='f1000000-0000-4000-8000-0000000000d4' where tenant_id='b0000001-0000-4000-8000-000000000001'::uuid and id='d0000001-0000-4000-8000-000000000002'::uuid;
select '=== 12 historico';
insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values('b0000001-0000-4000-8000-000000000001'::uuid,'REQUISICAO','d0000001-0000-4000-8000-000000000002'::uuid,'ENVIADA_PARA_APROVACAO',jsonb_build_object('ciclo',1,'total',40000,'etapas',1),'101','f1000000-0000-4000-8000-0000000000d4');
select '=== 13 idempotencia';
insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash) values('b0000001-0000-4000-8000-000000000001'::uuid,'REQUISICAO_ENVIAR','jornada-rq2-enviar-k1','d0000001-0000-4000-8000-000000000002'::uuid,md5('probe-enviar-rq2'));
select '=== 14 rollback (nada persistido)';
rollback;
