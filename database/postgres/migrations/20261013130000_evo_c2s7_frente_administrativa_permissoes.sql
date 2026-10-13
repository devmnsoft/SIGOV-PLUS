-- RC-EVO Ciclo 2 Sprint 7 (Mapa da frente administrativa - contratos de auditoria, sem
-- implementacao) - catalogo de permissoes da frente, contrato BR-7.1 a BR-7.8. Legislacao:
-- Lei 14.133/2021 (contratacoes publicas), LC 123/2006 (zona de preferencia ME/EPP).
-- Idempotente (regra 6); multi-esfera (regras 21-24): a frente vale para orgaos e entidades
-- municipais, estaduais e federais - nenhum ato, valor ou percentual e hardcoded aqui.
-- Escopo deliberadamente NAO funcional (gate da S7: documento de contratos revisado +
-- permissoes da frente catalogadas em seed + nenhuma implementacao fora do catalogo): esta
-- migration registra no catalogo oficial sigov.permissao (banco como autoridade - regra 11;
-- sem catalogo hardcoded - regra 12) uma chave canonica por ato da cadeia
-- demanda -> procedimento -> adjudicacao/homologacao -> contrato -> fornecimento ->
-- recebimento -> obrigacao financeira, e vincula o perfil sistemico SUPERADMIN. Os comandos
-- tipados que executam cada chave serao implementados nas Sprints 8-10 cumprindo o contrato
-- em docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md. Sem alteracao de schema:
-- somente dados do catalogo (manifest 232 -> 233 entradas; consolidados regenerados).
-- Chaves ja catalogadas e mantidas SEM alteracao (reuso documentado no contrato):
-- compras.requisicao.*, compras.processo.*, compras.cotacao.*, compras.julgamento.executar,
-- compras.contrato.{visualizar,criar}, compras.ata.*, compras.recebimento.executar,
-- compras.exportar, LICITACAO_*/CONTRATO_* legados (UPPER_SNAKE), almoxarifado.recebimento.confirmar,
-- patrimonio.incorporacao.executar e patrimonio.bem.criar.

-- 1) Catalogo das chaves canonicas dos atos novos da frente (BR-7.1 a BR-7.8).
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'compras',v.chave,v.recurso,v.acao,v.descricao,true,false from (values
 ('compras.demanda.visualizar','compras.demanda','visualizar','Visualizar demandas tipadas (item, quantidade, unidade, preco estimado, justificativa, centro de custo, urgencia) - BR-7.1'),
 ('compras.demanda.criar','compras.demanda','criar','Criar demanda tipada com itens e aprovacao; 1 demanda = 1 processo administrativo unico (Lei 14.133 art. 128) - BR-7.1'),
 ('compras.demanda.aprovar','compras.demanda','aprovar','Aprovar demanda (urgencia exige dupla aprovacao: ordenador + autoridade superior) - BR-7.1/BR-8.6'),
 ('compras.demanda.reabrir','compras.demanda','reabrir','Reabrir demanda repelida com justificativa; apos aprovacao a alteracao so e por nova versao imutavel - BR-7.1/BR-8.1'),
 ('compras.licitacao.abrir','compras.licitacao','abrir','Abrir procedimento: modalidade por objeto/valor (pregao eletronico default; concorrencia; consulta previa ate o teto vigente) - BR-7.2'),
 ('compras.licitacao.modalidade.definir','compras.licitacao.modalidade','definir','Definir modalidade e criterio do procedimento conforme objeto e valor - BR-7.2'),
 ('compras.licitacao.habilitacao.julgar','compras.licitacao.habilitacao','julgar','Julgar habilitacao (qualificacao juridica, fiscal/trabalhista, tecnica) - BR-7.2'),
 ('compras.licitacao.lances.registrar','compras.licitacao.lances','registrar','Realizar sessao de lances com zona de preferencias ME/EPP (LC 123/2006 - margem parametrizavel, default 5%) - BR-7.2'),
 ('compras.licitacao.preferencia.documentar','compras.licitacao.preferencia','documentar','Documentar direito de preferencia ME/EPP exercido ou desistencia fora da margem - BR-7.2'),
 ('compras.adjudicacao.executar','compras.adjudicacao','executar','Executar adjudicacao: ato separado e ordenado, antecedente obrigatorio da homologacao - BR-7.3'),
 ('compras.homologacao.executar','compras.homologacao','executar','Executar homologacao por ato superior distinto do adjudicador (separacao de funcoes); contrato so nasce de procedimento homologado - BR-7.3'),
 ('compras.contrato.aditivo.criar','compras.contrato.aditivo','criar','Criar aditivo quantitativo (percentual parametrizado por tipo de objeto) ou valorativo/reajuste com indice identificado - BR-7.4'),
 ('compras.contrato.aditivo.aprovar','compras.contrato.aditivo','aprovar','Aprovar aditivo contratual - BR-7.4'),
 ('compras.contrato.prorrogar','compras.contrato','prorrogar','Prorrogar contrato (Lei 14.133 art. 106 - ate 5 anos + 5 prorrogacoes, parametrico) - BR-7.4'),
 ('compras.contrato.rescindir','compras.contrato','rescindir','Rescindir contrato com ato e justificativa (unilateral por inadimplencia com notificacao; bilateral por comum acordo) - BR-7.4'),
 ('compras.contrato.execucao.fiscalizar','compras.contrato.execucao','fiscalizar','Controlar execucao financeira x quantitativa (%) e teto de fornecimentos - BR-7.4'),
 ('compras.fornecimento.criar','compras.fornecimento','criar','Criar pedido de fornecimento vinculado a item de contrato vigente (quantidade/valor disponiveis) - BR-7.5'),
 ('compras.fornecimento.visualizar','compras.fornecimento','visualizar','Visualizar pedidos de fornecimento contra contrato/ata - BR-7.5'),
 ('compras.recebimento.aprovar','compras.recebimento','aprovar','Aprovar recebimento parcial por fornecimento; total (aprovado+rejeitado = fornecido) fecha o fornecimento - BR-7.5'),
 ('compras.recebimento.rejeitar','compras.recebimento','rejeitar','Rejeitar item recebido com motivo e prazo de substituicao (parametrico, default 5 dias uteis); desfecho obrigatorio - BR-7.5'),
 ('compras.fatura.vincular','compras.fatura','vincular','Vincular NF-e/NFS-e ao fornecimento (fornecedor/CNPJ = contratado; conferencia de valor vs pedido) - BR-7.5'),
 ('compras.obrigacao.visualizar','compras.obrigacao','visualizar','Visualizar obrigacoes financeiras geradas por fornecimento aprovado - BR-7.7'),
 ('compras.obrigacao.liquidar','compras.obrigacao','liquidar','Liquidar obrigacao (alimenta tesouraria e compromisso orcamentario); pagamento so com liquida e documentos conferidos - BR-7.7'),
 ('compras.contrapartida.registrar','compras.contrapartida','registrar','Registrar contrapartida de recurso proprio quando o instrumento do convenio assim definir (percentual parametrico) - BR-7.8'),
 ('compras.auditoria.visualizar','compras.auditoria','visualizar','Visualizar trilha de auditoria ponta a ponta da frente (demanda -> processo -> contrato -> fornecimento -> recebimento -> destino -> obrigacao)'))
 v(chave,recurso,acao,descricao) where not exists(select 1 from sigov.permissao p where p.chave=v.chave);

-- 2) Vinculo dos perfis SUPERADMIN ativos as chaves novas (padrao FUNC03 com ajuste
-- documentado: o filtro original `and pa.sistemico` nao casa com o estado atual de
-- homologacao - as linhas SUPERADMIN tem sistemico=false no banco; a autoridade e o codigo
-- externo SUPERADMIN ativo e nao excluido, 1 perfil por tenant).
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false
 from sigov.perfil_acesso pa
 cross join sigov.permissao p
where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
  and p.modulo='compras' and p.ativo and not p.is_deleted
  and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir',
   'compras.licitacao.abrir','compras.licitacao.modalidade.definir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar',
   'compras.adjudicacao.executar','compras.homologacao.executar',
   'compras.contrato.aditivo.criar','compras.contrato.aditivo.aprovar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar',
   'compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular',
   'compras.obrigacao.visualizar','compras.obrigacao.liquidar','compras.contrapartida.registrar','compras.auditoria.visualizar')
on conflict (perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;

-- C2S7-POSTCOND: falha explicita se faltar qualquer artefato do incremento (regra 13)
do $$
declare
  n_chaves int;
  n_perfis int;
  n_vinculos int;
begin
  select count(1) into n_chaves
    from sigov.permissao
   where modulo='compras' and ativo and not is_deleted
     and chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir',
      'compras.licitacao.abrir','compras.licitacao.modalidade.definir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar',
      'compras.adjudicacao.executar','compras.homologacao.executar',
      'compras.contrato.aditivo.criar','compras.contrato.aditivo.aprovar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar',
      'compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular',
      'compras.obrigacao.visualizar','compras.obrigacao.liquidar','compras.contrapartida.registrar','compras.auditoria.visualizar');
  if n_chaves<>25 then
    raise exception 'C2S7-POSTCOND: catalogo de permissoes da frente incompleto (% <> 25)', n_chaves;
  end if;
  select count(1) into n_perfis from sigov.perfil_acesso where codigo_externo='SUPERADMIN' and ativo and not is_deleted;
  if n_perfis=0 then
    raise exception 'C2S7-POSTCOND: nenhum perfil SUPERADMIN ativo no banco (autoridade ausente)';
  end if;
  select count(1) into n_vinculos
    from sigov.perfil_permissao pp
    join sigov.permissao p on p.id=pp.permissao_id
    join sigov.perfil_acesso pa on pa.id=pp.perfil_acesso_id
   where pa.codigo_externo='SUPERADMIN' and pa.ativo and not pa.is_deleted
     and pp.ativo and not pp.is_deleted
     and p.chave in ('compras.demanda.visualizar','compras.demanda.criar','compras.demanda.aprovar','compras.demanda.reabrir',
      'compras.licitacao.abrir','compras.licitacao.modalidade.definir','compras.licitacao.habilitacao.julgar','compras.licitacao.lances.registrar','compras.licitacao.preferencia.documentar',
      'compras.adjudicacao.executar','compras.homologacao.executar',
      'compras.contrato.aditivo.criar','compras.contrato.aditivo.aprovar','compras.contrato.prorrogar','compras.contrato.rescindir','compras.contrato.execucao.fiscalizar',
      'compras.fornecimento.criar','compras.fornecimento.visualizar','compras.recebimento.aprovar','compras.recebimento.rejeitar','compras.fatura.vincular',
      'compras.obrigacao.visualizar','compras.obrigacao.liquidar','compras.contrapartida.registrar','compras.auditoria.visualizar');
  if n_vinculos<>n_perfis*25 then
    raise exception 'C2S7-POSTCOND: vinculos SUPERADMIN da frente incompletos (% <> %*25)', n_vinculos, n_perfis;
  end if;
end
$$;
