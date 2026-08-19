begin;
set search_path to sigov;

-- Catálogo granular do eixo documental e institucional. A autorização efetiva
-- continua vinculada ao tenant e aos módulos habilitados.
insert into sigov.permissao (modulo, chave, descricao)
select p.modulo, p.chave, p.descricao
from (values
 ('processos','processos.dashboard.visualizar','Visualizar dashboard de processos'),
 ('processos','processos.protocolo.visualizar','Visualizar protocolo'),
 ('processos','processos.protocolo.criar','Criar protocolo'),
 ('processos','processos.protocolo.alterar','Alterar protocolo'),
 ('processos','processos.protocolo.cancelar','Cancelar protocolo'),
 ('processos','processos.processo.visualizar','Visualizar processo'),
 ('processos','processos.processo.autuar','Autuar processo'),
 ('processos','processos.processo.tramitar','Tramitar processo'),
 ('processos','processos.processo.despachar','Despachar processo'),
 ('processos','processos.processo.encerrar','Encerrar processo'),
 ('processos','processos.processo.reabrir','Reabrir processo'),
 ('processos','processos.processo.cancelar','Cancelar processo'),
 ('processos','processos.processo.exportar','Exportar processos'),
 ('processos','processos.documento.anexar','Anexar documento ao processo'),
 ('processos','processos.documento.visualizar','Visualizar documento do processo'),
 ('processos','processos.documento.sigilo_visualizar','Visualizar documento sigiloso do processo'),
 ('ged','ged.dashboard.visualizar','Visualizar dashboard GED'),
 ('ged','ged.documento.visualizar','Visualizar documento GED'),
 ('ged','ged.documento.criar','Criar documento GED'),
 ('ged','ged.documento.alterar','Alterar metadados GED'),
 ('ged','ged.documento.versionar','Versionar documento GED'),
 ('ged','ged.documento.classificar','Classificar documento GED'),
 ('ged','ged.documento.definir_sigilo','Definir sigilo de documento GED'),
 ('ged','ged.documento.descartar_preparatorio','Preparar descarte documental'),
 ('ged','ged.caixa.visualizar','Visualizar caixa documental'),
 ('ged','ged.caixa.criar','Criar caixa documental'),
 ('ged','ged.pasta.visualizar','Visualizar pasta documental'),
 ('ged','ged.pasta.criar','Criar pasta documental'),
 ('ged','ged.temporalidade.visualizar','Visualizar temporalidade'),
 ('ged','ged.temporalidade.gerenciar','Gerenciar temporalidade'),
 ('ged','ged.relatorio.exportar','Exportar relatório GED'),
 ('assinatura','assinatura.documento.visualizar','Visualizar documento para assinatura'),
 ('assinatura','assinatura.documento.solicitar','Solicitar assinatura'),
 ('assinatura','assinatura.documento.assinar','Assinar documento'),
 ('assinatura','assinatura.documento.rejeitar','Rejeitar assinatura'),
 ('assinatura','assinatura.documento.validar','Validar assinatura'),
 ('assinatura','assinatura.documento.cancelar_solicitacao','Cancelar solicitação de assinatura'),
 ('assinatura','assinatura.relatorio.exportar','Exportar relatório de assinaturas'),
 ('legislativo','legislativo.dashboard.visualizar','Visualizar dashboard legislativo'),
 ('legislativo','legislativo.materia.visualizar','Visualizar matéria'),
 ('legislativo','legislativo.materia.criar','Criar matéria'),
 ('legislativo','legislativo.materia.tramitar','Tramitar matéria'),
 ('legislativo','legislativo.materia.aprovar','Aprovar matéria'),
 ('legislativo','legislativo.materia.rejeitar','Rejeitar matéria'),
 ('legislativo','legislativo.sessao.visualizar','Visualizar sessão'),
 ('legislativo','legislativo.sessao.criar','Criar sessão'),
 ('legislativo','legislativo.sessao.encerrar','Encerrar sessão'),
 ('legislativo','legislativo.votacao.registrar','Registrar votação'),
 ('legislativo','legislativo.ata.gerar','Gerar ata'),
 ('legislativo','legislativo.publicacao.enviar','Enviar matéria para publicação'),
 ('legislativo','legislativo.relatorio.exportar','Exportar relatório legislativo'),
 ('diario','diario.dashboard.visualizar','Visualizar dashboard do diário'),
 ('diario','diario.edicao.visualizar','Visualizar edição do diário'),
 ('diario','diario.edicao.criar','Criar edição do diário'),
 ('diario','diario.edicao.publicar','Publicar edição do diário'),
 ('diario','diario.materia.incluir','Incluir matéria no diário'),
 ('diario','diario.materia.remover','Remover matéria do diário'),
 ('diario','diario.publicacao.validar','Validar publicação do diário'),
 ('transparencia','transparencia.dashboard.visualizar','Visualizar dashboard da transparência'),
 ('transparencia','transparencia.publicacao.visualizar','Visualizar publicação'),
 ('transparencia','transparencia.publicacao.publicar','Publicar item na transparência'),
 ('transparencia','transparencia.publicacao.remover','Remover publicação da transparência'),
 ('transparencia','transparencia.relatorio.exportar','Exportar relatório da transparência'),
 ('atendimento','atendimento.ouvidoria.visualizar','Visualizar manifestação'),
 ('atendimento','atendimento.ouvidoria.criar','Criar manifestação'),
 ('atendimento','atendimento.ouvidoria.encaminhar','Encaminhar manifestação'),
 ('atendimento','atendimento.ouvidoria.responder','Responder manifestação'),
 ('atendimento','atendimento.ouvidoria.prorrogar','Prorrogar manifestação'),
 ('atendimento','atendimento.ouvidoria.encerrar','Encerrar manifestação'),
 ('atendimento','atendimento.esic.visualizar','Visualizar pedido e-SIC'),
 ('atendimento','atendimento.esic.criar','Criar pedido e-SIC'),
 ('atendimento','atendimento.esic.encaminhar','Encaminhar pedido e-SIC'),
 ('atendimento','atendimento.esic.responder','Responder pedido e-SIC'),
 ('atendimento','atendimento.esic.prorrogar','Prorrogar pedido e-SIC'),
 ('atendimento','atendimento.esic.recurso','Registrar recurso e-SIC'),
 ('atendimento','atendimento.esic.encerrar','Encerrar pedido e-SIC'),
 ('atendimento','atendimento.relatorio.exportar','Exportar relatório de atendimento')
) p(modulo, chave, descricao)
where not exists (
 select 1 from sigov.permissao atual
 where atual.modulo = p.modulo and atual.chave = p.chave
);

insert into sigov.perfil_acesso (nome, descricao, codigo_externo)
select p.nome, p.descricao, p.codigo
from (values
 ('Gestor Documental','Gestão de classificação, temporalidade e acervo GED','GESTOR_DOCUMENTAL'),
 ('Atendimento e Protocolo','Abertura e encaminhamento inicial de solicitações','ATENDIMENTO_PROTOCOLO'),
 ('Servidor Setorial','Tramitação, despacho e anexação no setor autorizado','SERVIDOR_SETORIAL'),
 ('Assinador','Assinatura ou rejeição de documentos atribuídos','ASSINADOR'),
 ('Publicador','Preparação e publicação institucional','PUBLICADOR'),
 ('Gestor Legislativo','Gestão de matérias, sessões, votações e atas','GESTOR_LEGISLATIVO'),
 ('Ouvidoria','Tratamento de manifestações no escopo autorizado','OUVIDORIA'),
 ('e-SIC','Tratamento de pedidos de acesso à informação','ESIC')
) p(nome, descricao, codigo)
where not exists (
 select 1 from sigov.perfil_acesso atual
 where atual.codigo_externo = p.codigo and atual.is_deleted = false
);

commit;
