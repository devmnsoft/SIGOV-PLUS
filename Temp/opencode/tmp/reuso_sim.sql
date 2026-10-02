begin;
update sigov.compras_empresarial_aprovacao_politica set ativo=true,is_deleted=false,version=version+1,updated_at=now(),updated_by='u',correlation_id='c' where tenant_id='b0000001-0000-4000-8000-000000000001' and id=7;
update sigov.compras_empresarial_aprovacao_politica_nivel set is_deleted=true,updated_at=now(),updated_by='u' where tenant_id='b0000001-0000-4000-8000-000000000001' and politica_id=7 and not is_deleted;
insert into sigov.compras_empresarial_aprovacao_politica_nivel(tenant_id,politica_id,ordem,limite,created_by,updated_by,correlation_id) values('b0000001-0000-4000-8000-000000000001',7,1,50000,'u','u','c') on conflict (tenant_id,politica_id,ordem) do update set limite=50000,is_deleted=false,updated_at=now(),updated_by='u',correlation_id='c';
insert into sigov.compras_empresarial_aprovacao_politica_nivel(tenant_id,politica_id,ordem,limite,created_by,updated_by,correlation_id) values('b0000001-0000-4000-8000-000000000001',7,2,600000,'u','u','c') on conflict (tenant_id,politica_id,ordem) do update set limite=600000,is_deleted=false,updated_at=now(),updated_by='u',correlation_id='c';
select id,ordem,limite,is_deleted from sigov.compras_empresarial_aprovacao_politica_nivel where politica_id=7 order by ordem;
rollback;
