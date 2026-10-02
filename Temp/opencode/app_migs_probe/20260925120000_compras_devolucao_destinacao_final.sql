-- Conclusão da jornada física de devoluções e acompanhamento de destinação dos itens rejeitados.
do $$ begin
 if not exists(select 1 from pg_constraint where conname='fk_comp_dev_responsavel') then
  alter table sigov.compras_empresarial_devolucao add constraint fk_comp_dev_responsavel foreign key(tenant_id,responsavel_id) references sigov.os_tecnico(tenant_id,usuario_id);
 end if;
end $$;

create index if not exists ix_comp_dev_responsavel on sigov.compras_empresarial_devolucao(tenant_id,responsavel_id);
create index if not exists ix_comp_dev_datas on sigov.compras_empresarial_devolucao(tenant_id,expedida_em,entregue_em);
create index if not exists ix_comp_dev_item_destinacao on sigov.compras_empresarial_devolucao_item(tenant_id,recebimento_item_id,devolucao_id);

do $$ begin
 if not exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='devolucao_id') then
  alter table sigov.compras_empresarial_recebimento_divergencia add column devolucao_id bigint;
  alter table sigov.compras_empresarial_recebimento_divergencia add constraint fk_comp_div_devolucao foreign key(tenant_id,devolucao_id) references sigov.compras_empresarial_devolucao(tenant_id,id);
  create index if not exists ix_comp_div_devolucao on sigov.compras_empresarial_recebimento_divergencia(tenant_id,devolucao_id);
 end if;
end $$;

insert into sigov.permissao(modulo,chave,descricao,ativo) select 'COMPRAS_EMPRESARIAIS',x.chave,x.descricao,true from(values
 ('compras_empresariais.devolucoes.visualizar','Visualizar devoluções físicas'),
 ('compras_empresariais.devolucoes.criar','Preparar devoluções físicas'),
 ('compras_empresariais.devolucoes.editar','Editar ou cancelar rascunhos de devolução'),
 ('compras_empresariais.devolucoes.expedir','Confirmar saída física de devoluções'),
 ('compras_empresariais.devolucoes.entregar','Confirmar entrega física ao fornecedor'),
 ('compras_empresariais.devolucoes.relatorio','Exportar relatório de devoluções'))x(chave,descricao)
on conflict(chave) do update set modulo=excluded.modulo,descricao=excluded.descricao,ativo=true,is_deleted=false;
