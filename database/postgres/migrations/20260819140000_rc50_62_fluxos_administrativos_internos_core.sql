begin;
set search_path to sigov;

-- Catálogo administrativo granular. O vínculo efetivo continua por tenant,
-- módulo contratado e perfil; esta migration não concede acesso implicitamente.
insert into sigov.permissao (modulo, chave, descricao)
select split_part(chave,'.',1), chave, initcap(replace(chave,'.',' '))
from unnest(array[
'rh.dashboard.visualizar','rh.servidor.visualizar','rh.servidor.criar','rh.servidor.alterar','rh.servidor.inativar','rh.vinculo.visualizar','rh.vinculo.criar','rh.vinculo.alterar','rh.lotacao.visualizar','rh.lotacao.alterar','rh.ferias.visualizar','rh.ferias.registrar','rh.afastamento.visualizar','rh.afastamento.registrar',
'folha.dashboard.visualizar','folha.evento.visualizar','folha.evento.criar','folha.evento.alterar','folha.fechamento.executar','folha.fechamento.reabrir','folha.relatorio.exportar',
'compras.dashboard.visualizar','compras.requisicao.visualizar','compras.requisicao.criar','compras.requisicao.aprovar','compras.requisicao.reprovar','compras.cotacao.visualizar','compras.cotacao.criar','compras.processo.visualizar','compras.processo.criar','compras.processo.cancelar',
'licitacao.dashboard.visualizar','licitacao.processo.visualizar','licitacao.processo.criar','licitacao.processo.homologar','licitacao.processo.cancelar',
'contrato.dashboard.visualizar','contrato.contrato.visualizar','contrato.contrato.criar','contrato.contrato.alterar','contrato.contrato.encerrar','contrato.aditivo.criar','contrato.fiscal.definir','contrato.medicao.registrar','contrato.relatorio.exportar',
'almoxarifado.dashboard.visualizar','almoxarifado.material.visualizar','almoxarifado.material.criar','almoxarifado.entrada.registrar','almoxarifado.saida.registrar','almoxarifado.requisicao.criar','almoxarifado.requisicao.aprovar','almoxarifado.relatorio.exportar',
'patrimonio.dashboard.visualizar','patrimonio.bem.visualizar','patrimonio.bem.tombar','patrimonio.bem.transferir','patrimonio.bem.baixar','patrimonio.inventario.executar','patrimonio.relatorio.exportar',
'frotas.dashboard.visualizar','frotas.veiculo.visualizar','frotas.veiculo.criar','frotas.veiculo.alterar','frotas.motorista.visualizar','frotas.motorista.criar','frotas.abastecimento.registrar','frotas.manutencao.registrar','frotas.ocorrencia.registrar','frotas.relatorio.exportar',
'obras.dashboard.visualizar','obras.obra.visualizar','obras.obra.criar','obras.obra.alterar','obras.diario.registrar','obras.medicao.registrar','obras.ocorrencia.registrar','obras.paralisacao.registrar','obras.retomada.registrar','obras.encerramento.registrar','obras.relatorio.exportar'
]::text[]) as p(chave)
where not exists (select 1 from sigov.permissao atual where atual.chave=p.chave);

insert into sigov.perfil_acesso (nome, descricao, codigo_externo)
select p.nome,p.descricao,p.codigo
from (values
('Secretário de Administração','Visão gerencial e aprovações administrativas','SECRETARIO_ADMINISTRACAO'),
('Coordenador RH','Coordenação de servidores, vínculos, lotações e afastamentos','COORDENADOR_RH'),
('Funcionário RH','Operação autorizada de recursos humanos','FUNCIONARIO_RH'),
('Funcionário Folha','Eventos e fechamento preparatório da folha','FUNCIONARIO_FOLHA'),
('Coordenador Compras','Coordenação de requisições, processos e contratos','COORDENADOR_COMPRAS'),
('Operador Compras','Operação de requisições, cotações e processos','OPERADOR_COMPRAS'),
('Fiscal de Contrato','Fiscalização, medição e ocorrência contratual','FISCAL_CONTRATO'),
('Almoxarifado','Operação de materiais e estoque','ALMOXARIFADO'),
('Patrimônio','Tombamento, inventário, transferência e baixa','PATRIMONIO'),
('Coordenador Frotas','Gestão da frota e custos','COORDENADOR_FROTAS'),
('Operador Frotas','Registro de abastecimento, manutenção e ocorrência','OPERADOR_FROTAS'),
('Fiscal de Obras','Diário, medição e fiscalização de obras','FISCAL_OBRAS'),
('Funcionário Financeiro','Operação financeira segregada das origens','FUNCIONARIO_FINANCEIRO')
) p(nome,descricao,codigo)
where not exists (select 1 from sigov.perfil_acesso atual where atual.codigo_externo=p.codigo and atual.is_deleted=false);

commit;
