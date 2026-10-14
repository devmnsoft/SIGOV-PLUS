namespace Sigov.Application.Compras.Integradas;

// RC-EVO Ciclo 2 Sprint 8 (BR-8.1 a BR-8.7 - Compras integradas 1/2: demanda -> contratacao -> contrato).
// Contratos tipados da frente publica multi-esfera consumindo as chaves de permissao da seed C2S7
// (banco = autoridade, regras 11/12) e os parametros canonicos do modulo COMPRAS no banco (regra 11).
// Falhas nomeadas conforme docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md (regra 13).
public sealed record ComprasIntegradasContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string CorrelationId);

// ------------------------------- requests ---------------------------------
public sealed record DemandaItemInput(string Descricao, decimal Quantidade, string Unidade, string? TipoObjeto, decimal PrecoEstimado, long? PesquisaPrecoId, long CentroCustoId);
public sealed record DemandaCriarRequest(string Objeto, string Justificativa, IReadOnlyList<DemandaItemInput> Itens, bool Urgencia = false, string? JustificativaUrgencia = null);
public sealed record DemandaOperarRequest(string Justificativa);
public sealed record ProcedimentoAbrirRequest(long DemandaId, string ModalidadeCodigo, decimal? OrcamentoEstimado = null, string? FonteRecurso = null, string? DispensaBaseLegalCodigo = null);
public sealed record HabilitacaoJulgarRequest(long FornecedorId, string Tipo, string DocumentoRef, string Status, string? Justificativa = null);
public sealed record LanceRegistrarRequest(long FornecedorId, decimal Valor, string? ChaveIdempotencia = null);
public sealed record PreferenciaDocumentarRequest(long FornecedorId, string DocumentoRef);
public sealed record AdjudicarRequest(long? FornecedorId = null, string? Observacao = null);
public sealed record ContratoGerarRequest(long HomologacaoId, DateOnly DataEficacia, DateOnly DataFim, string AssinaturaModo, string? Gestor = null, string? Fiscal = null, string? Numero = null, long? ObraId = null);
// Sprint 9 (BR-7.5 a BR-7.7): fornecimento -> recebimento -> fatura -> destinacao -> obrigacao
public sealed record FornecimentoCriarRequest(long ContratoId, long ContratoItemId, decimal Quantidade, string? ChaveIdempotencia = null);
public sealed record RecebimentoIntegradoRegistrarRequest(decimal QuantidadeAprovada, decimal QuantidadeRejeitada, DateOnly DataRecebimento, string? MotivoRejeicao = null);
public sealed record RecebimentoDesfechoRequest(string Desfecho, string Justificativa);
public sealed record FaturaVincularRequest(string Numero, string? Serie, string Natureza, string CnpjFornecedor, decimal Valor, DateOnly DataEmissao);
public sealed record DestinacaoIntegradaRequest(long FornecimentoRecebimentoId, string AlvoTipo, decimal Quantidade, string Referencia, long? LocalId = null, long? MaterialId = null, long? CategoriaId = null, string? TipoBem = null, string? Localizacao = null);
public sealed record ObrigacaoGerarRequest(long FornecimentoId, string ReferenciaIdempotencia);
// Sprint 10 (BR-7.8 + BR-10.x): vinculos juridico + convenios/obras (etapa 3/3)
public sealed record ConsultaJuridicaCriarRequest(long ProcessoComprasId, string TipoConsulta, string ReferenciaNumero, string Descricao, string SecretariaSetor, DateOnly DataSolicitacao, string? Solicitante = null, DateOnly? PrazoResposta = null, decimal CustoHonorario = 0m, string? ChaveIdempotencia = null);
public sealed record ParecerEmitirRequest(string Conclusao, string Resumo, IReadOnlyList<string>? Condicoes = null);
public sealed record ContenciosoAbrirRequest(string OrigemTipo, long OrigemId, string Caso, decimal ValorCausa, string Assunto, string? Instancia = null, string? VaraOrgao = null, string? ReferenciaExterna = null);
public sealed record ContenciosoDesfecharRequest(string Resultado, decimal Custas, string Justificativa);
public sealed record ContrapartidaRegistrarRequest(long InstrumentoId, string Tipo, string Descricao, decimal ValorPrevisto, decimal ValorRealizado = 0m, DateOnly? DataRealizacao = null, string? ChaveIdempotencia = null);
public sealed record VinculoConvenioAplicarRequest(long ContratoId, long InstrumentoId, string? ChaveIdempotencia = null);
public sealed record PrestacaoRegistrarRequest(long InstrumentoId, string Tipo, string NumeroPrestacao, DateOnly PeriodoInicial, DateOnly PeriodoFinal, decimal ValorPrestado, string? ParecerResumo = null);

// --------------------------------- dtos -----------------------------------
public sealed record DemandaIntegradaDto(long Id, string Objeto, string Status, bool Urgencia, decimal ValorEstimado, int NItens, long? CentroCustoId, long? ProcessoId, long CriadoPor, long? AprovadoPor, long? DuplaAprovadoPor, DateTimeOffset CriadoEm);
public sealed record ProcedimentoAbertoDto(long ProcessoId, long LicitacaoId, string Modalidade, string Criterio, bool JaExiste);
public sealed record PreferenciaDesfechoDto(string ZonaStatus, string Desfecho, bool DentroDaMargem, decimal MelhorLanceMeEpp, decimal? MelhorLanceForaMeEpp, int MargemPct);
public sealed record ClassificacaoItemDto(int Ordem, long FornecedorId, decimal Valor, string Status);
public sealed record ClassificacaoDto(IReadOnlyList<ClassificacaoItemDto> Itens, string? JustificativaEmpate);
public sealed record AdjudicacaoDto(long AdjudicacaoId, long PropostaVencedoraId, long FornecedorId, decimal ValorTotal, int NItens);
public sealed record PropostaIntegradaDto(long Id, long FornecedorId, decimal Valor, int? OrdemClassificacao, string Status);
public sealed record ContratoGeradoDto(long ContratoId, string Numero, long FornecedorId, decimal ValorContratado, int NItens, DateOnly DataEficacia, DateOnly DataFim, string AssinaturaModo, string Status);
public sealed record ContratoExecucaoDto(long ContratoId, decimal ValorContratado, decimal TotalFornecido, decimal PctExecutado, int TetoPct, bool BloqueiaNovoFornecimento, bool ContenciosoAtivo = false);
// Sprint 9 (BR-7.5 a BR-7.7)
public sealed record FornecimentoIntegradoDto(long Id, long ContratoId, long ContratoItemId, long FornecedorId, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal, string Status);
public sealed record RecebimentoIntegradoDto(long Id, long FornecimentoId, decimal QuantidadeAprovada, decimal QuantidadeRejeitada, string Status, DateOnly? DataPrazoSubstituicao, string? Desfecho);
public sealed record FaturaIntegradaDto(long Id, long FornecimentoId, string Numero, string Natureza, decimal Valor, bool Conferida);
public sealed record DestinacaoIntegradaDto(long Id, long FornecimentoRecebimentoId, string AlvoTipo, decimal Quantidade, decimal Valor, string Referencia, long? AlvoId);
public sealed record ObrigacaoFinanceiraDto(long Id, long FornecimentoId, string ReferenciaIdempotencia, decimal ValorLiquidado, string Status);
// Sprint 10 (BR-7.8 + BR-10.x)
public sealed record ConsultaJuridicaDto(long ConsultaId, long ProcessoComprasId, string TipoConsulta, string Status, string? Conclusao, int CondicoesAbertas, long? ConsultaPaiId, string ReferenciaNumero);
public sealed record CondicaoSatisfeitaDto(long CondicaoId, long ConsultaId, int CondicoesAbertas, string ConsultaStatus);
public sealed record ContenciosoDto(long ProcessoId, string OrigemTipo, long OrigemId, string Caso, string Status, string? Resultado, int Movimentos);
public sealed record ContrapartidaDto(long Id, long InstrumentoId, string Tipo, string Descricao, decimal ValorPrevisto, decimal ValorRealizado, string Status, string? ChaveIdempotencia);
public sealed record VinculoExternoDto(long Id, long ContratoId, string AlvoModulo, long AlvoId, string Status, string? ChaveIdempotencia, string? JustificativaQuitacao);
public sealed record ConvencioEncerradoDto(long InstrumentoId, string Status, decimal SaldoExecucao);
public sealed record PrestacaoDto(long Id, long InstrumentoId, string Tipo, string NumeroPrestacao, decimal ValorPrestado, string Status);
public sealed record MedicaoHomologadaDto(long MedicaoId, long ObraId, decimal Valor, decimal PercentualFisico, string Status, decimal SaldoContratualDepois);

// --------------------------- falhas nomeadas ------------------------------
public class ComprasIntegradasFalhaNomeada(string mensagem) : Exception(mensagem) { }

// 404 - recursos ausentes
public sealed class DemandaInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"DEMANDA_INEXISTENTE: demanda {id} nao encontrada no tenant.");
public sealed class LicitacaoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"LICITACAO_INEXISTENTE: licitacao {id} nao encontrada no tenant.");
public sealed class PropostaInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"PROPOSTA_INEXISTENTE: proposta {id} nao pertence a licitacao ou ao tenant.");
public sealed class ContratoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"CONTRATO_INEXISTENTE: contrato {id} nao encontrado no tenant.");
public sealed class HomologacaoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"HOMOLOGACAO_INEXISTENTE: homologacao {id} nao encontrada no tenant.");
public sealed class FornecedorInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"FORNECEDOR_INEXISTENTE: fornecedor {id} nao encontrado no tenant.");
public sealed class ModalidadeForaDoCatalogoException(string codigo) : ComprasIntegradasFalhaNomeada($"MODALIDADE_FORA_DO_CATALOGO: modalidade '{codigo}' ausente do catalogo do banco para o tenant/esfera.");
public sealed class DispensaBaseLegalInexistenteException(string codigo) : ComprasIntegradasFalhaNomeada($"DISPENSA_BASE_LEGAL_INEXISTENTE: base legal '{codigo}' ausente do catalogo art. 75 no banco.");
public sealed class ComprasIntegradasRecursoNaoDisponivelException(string tabela) : ComprasIntegradasFalhaNomeada($"COMPRAS_INTEGRADAS_NAO_DISPONIVEL: schema ausente (sigov.{tabela}); aplique a migration 20261013140000.");

// 400 - falhas de entrada/regra simples
public sealed class DemandaItemInvalidoException(string detalhe) : ComprasIntegradasFalhaNomeada($"DEMANDA_ITEM_INVALIDO: {detalhe}");
public sealed class DemandaUrgenciaSemJustificativaException() : ComprasIntegradasFalhaNomeada($"DEMANDA_URGENCIA_SEM_JUSTIFICATIVA: urgencia exige justificativa expandida.");
public sealed class ConsultaPreviaAcimaTetoException(decimal total, decimal teto) : ComprasIntegradasFalhaNomeada($"CONSULTA_PREVIA_ACIMA_TETO: valor {total:F2} excede o teto parametrizado {teto:F2} (parametro COMPRAS_CONSULTA_PREVIA_TETO).");
public sealed class DispensaBaseLegalObrigatoriaException() : ComprasIntegradasFalhaNomeada($"DISPENSA_BASE_LEGAL_OBRIGATORIA: modalidade direta (dispensa/inexigibilidade) exige base legal selecionada do catalogo art. 75.");
public sealed class HabilitacaoReprovacaoSemJustificativaException(long fornecedorId, string tipo) : ComprasIntegradasFalhaNomeada($"HABILITACAO_REPROVACAO_SEM_JUSTIFICATIVA: reprovacao do tipo '{tipo}' do fornecedor {fornecedorId} exige justificativa.");
public sealed class JulgamentoEmpateSemJustificativaException() : ComprasIntegradasFalhaNomeada($"JULGAMENTO_EMPATE_SEM_JUSTIFICATIVA: empate de classificacao exige decisao registrada (justificativaEmpate).");
public sealed class AdjudicacaoDiretaSemFornecedorException() : ComprasIntegradasFalhaNomeada($"ADJUDICACAO_DIRETA_SEM_FORNECEDOR: adjudicacao de modalidade direta (dispensa/inexigibilidade) exige fornecedorId.");
public sealed class AdjudicacaoSemItensLicitadosException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"ADJUDICACAO_SEM_ITENS_LICITADOS: licitacao {licitacaoId} sem itens (compras_licitacao_item) para espelhar na adjudicacao.");
public sealed class RescisaoSemAtoJustificativaException(string detalhe) : ComprasIntegradasFalhaNomeada($"RESCISAO_SEM_ATO_JUSTIFICATIVA: {detalhe}");

// 409 - conflitos de estado/invariante
public sealed class DemandaEstadoInvalidoException(long id, string statusAtual, string acao) : ComprasIntegradasFalhaNomeada($"DEMANDA_ESTADO_INVALIDO: demanda {id} em '{statusAtual}' nao aceita acao '{acao}'.");
public sealed class DemandaUrgenciaSemDuplaAprovacaoException(long id) : ComprasIntegradasFalhaNomeada($"DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO: demanda urgente {id} aguarda a segunda aprovacao por autoridade distinta.");
public sealed class DemandaAposAprovacaoImutavelException(long id) : ComprasIntegradasFalhaNomeada($"DEMANDA_APOS_APROVACAO_IMUTAVEL: demanda {id} aprovada e imutavel; alteracao somente por nova versao.");
public sealed class SeparacaoFuncoesVioladaException(string detalhe) : ComprasIntegradasFalhaNomeada($"SEPARACAO_FUNCOES_VIOLADA: {detalhe}");
public sealed class ProcessoJaVinculadoADemandaException(long demandaId, string modalidadeAtual) : ComprasIntegradasFalhaNomeada($"PROCESSO_JA_VINCULADO_A_DEMANDA: a demanda {demandaId} ja abriu procedimento com modalidade '{modalidadeAtual}' no processo unico (Lei 14.133 art. 128).");
public sealed class LicitacaoEstadoInvalidoException(long id, string statusAtual, string acao) : ComprasIntegradasFalhaNomeada($"LICITACAO_ESTADO_INVALIDO: licitacao {id} em '{statusAtual}' nao aceita acao '{acao}'.");
public sealed class SessaoLanceForaDeVigenciaException(long licitacaoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"SESSAO_LANCE_FORA_DE_VIGENCIA: sessao da licitacao {licitacaoId} em '{statusAtual}' nao recebe lances.");
public sealed class LanceNaoDecrescenteException(decimal valor, decimal melhorAtual) : ComprasIntegradasFalhaNomeada($"LANCE_NAO_DECRESCENTE: lance {valor:F2} nao e decrescente em relacao ao melhor atual {melhorAtual:F2}.");
public sealed class LanceChaveIdempotenciaConflitoException(string chave, long lanceId) : ComprasIntegradasFalhaNomeada($"LANCE_CHAVE_IDEMPOTENCIA_CONFLITO: chave '{chave}' ja registrada no lance {lanceId} com conteudo diferente.");
public sealed class ZonaMeEppNaoAplicavelException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"ZONA_ME_EPP_NAO_APLICAVEL: licitacao {licitacaoId} sem lancistas ME/EPP - zona de preferencias nao se aplica.");
public sealed class ZonaMeEppJaResolvidaException(long licitacaoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"ZONA_ME_EPP_JA_RESOLVIDA: zona da licitacao {licitacaoId} ja encerrada ('{statusAtual}'); documentacao so uma vez.");
public sealed class FornecedorForaDaZonaMeEppException(long fornecedorId) : ComprasIntegradasFalhaNomeada($"FORNECEDOR_FORA_DA_ZONA_ME_EPP: fornecedor {fornecedorId} nao e classificado ME/EPP no cadastro.");
public sealed class JulgamentoJaRealizadoException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"JULGAMENTO_JA_REALIZADO: licitacao {licitacaoId} ja classificou propostas.");
public sealed class JulgamentoSemLancesException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"JULGAMENTO_SEM_LANCES: licitacao {licitacaoId} sem lances registrados.");
public sealed class PreferenciaMeEppNaoDocumentadaException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"PREFERENCIA_ME_EPP_NAO_DOCUMENTADA: zona ME/EPP da licitacao {licitacaoId} ainda aberta - documente o desfecho antes do julgamento.");
public sealed class AdjudicacaoSemJulgamentoException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"ADJUDICACAO_SEM_JULGAMENTO: licitacao {licitacaoId} competitiva requer classificacao previa (julgamento).");
public sealed class AdjudicacaoJaRealizadaException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"ADJUDICACAO_JA_REALIZADA: licitacao {licitacaoId} ja foi adjudicada.");
public sealed class AdjudicacaoSemPropostaClassificadaException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"ADJUDICACAO_SEM_PROPOSTA_CLASSIFICADA: licitacao {licitacaoId} sem proposta classificada vigente (todas rejeitadas?).");
public sealed class HomologacaoSemAdjudicacaoException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"HOMOLOGACAO_SEM_ADJUDICACAO: licitacao {licitacaoId} sem adjudicacao registrada.");
public sealed class HomologacaoJaRealizadaException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"HOMOLOGACAO_JA_REALIZADA: licitacao {licitacaoId} ja foi homologada.");
public sealed class HomologadorEEadjudicadorIguaisException(long usuarioId) : ComprasIntegradasFalhaNomeada($"HOMOLOGADOR_E_ADJUDICADOR_IGUAIS: separacao de funcoes - o usuario {usuarioId} nao pode homologar o que adjudicou.");
public sealed class HomologacaoPrazoExcedidoException(long licitacaoId, double diasDecorridos, int prazoMaxDias) : ComprasIntegradasFalhaNomeada($"HOMOLOGACAO_PRAZO_EXCEDIDO: licitacao {licitacaoId} homologada apos {diasDecorridos:F1} dias da adjudicacao (maximo parametrico {prazoMaxDias}).");
public sealed class ContratoJaGeradoParaHomologacaoException(long homologacaoId, long contratoId) : ComprasIntegradasFalhaNomeada($"CONTRATO_JA_GERADO_PARA_HOMOLOGACAO: homologacao {homologacaoId} ja originou o contrato vigente {contratoId}.");
public sealed class ContratoSemProcedimentoHomologadoException(long homologacaoId) : ComprasIntegradasFalhaNomeada($"CONTRATO_SEM_PROCEDIMENTO_HOMOLOGADO: contrato so nasce de homologacao com adjudicacao (homologacao {homologacaoId}).");
public sealed class ContratoNaoVigenteException(long contratoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"CONTRATO_NAO_VIGENTE: contrato {contratoId} em '{statusAtual}' nao aceita a operacao.");
public sealed class ProrrogacaoExcedeTetoArt106Exception(long contratoId, DateOnly novoFim, DateOnly limite) : ComprasIntegradasFalhaNomeada($"PRORROGACAO_EXCEDE_TETO_ART_106: contrato {contratoId} prorrogado ate {novoFim} excede o teto parametrico (art. 106) em {limite}.");
public sealed class ProrrogacoesMaximasExcedidasException(long contratoId, int vezes, int maximo) : ComprasIntegradasFalhaNomeada($"PRORROGACOES_MAXIMAS_EXCEDIDAS: contrato {contratoId} ja prorrogado {vezes}x (maximo parametrico {maximo}, art. 106).");
public sealed class FornecimentoExcedeExecucaoDisponivelException(long contratoId, decimal pctExecutado, int tetoPct) : ComprasIntegradasFalhaNomeada($"FONTECIMENTO_EXCEDE_EXECUCAO_DISPONIVEL: contrato {contratoId} executado em {pctExecutado:F2}% (teto parametrico {tetoPct}%).");
public sealed class PropostaJaRejeitadaException(long propostaId) : ComprasIntegradasFalhaNomeada($"PROPOSTA_JA_REJEITADA: proposta {propostaId} ja foi rejeitada como inexequivel.");
public sealed class PropostaRejeicaoSemJulgamentoException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"PROPOSTA_REJEICAO_SEM_JULGAMENTO: licitacao {licitacaoId} ainda nao classificou propostas.");
public sealed class PropostaRejeicaoPosAdjudicacaoException(long licitacaoId) : ComprasIntegradasFalhaNomeada($"PROPOSTA_REJEICAO_POS_ADJUDICACAO: licitacao {licitacaoId} ja adjudicada - rejeicao so entre classificacao e adjudicacao.");
// Sprint 9 (BR-7.5 a BR-7.7)
public sealed class FornecimentoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"FORNECIMENTO_INEXISTENTE: fornecimento {id} nao encontrado no tenant.");
public sealed class ContratoItemInexistenteException(long itemId) : ComprasIntegradasFalhaNomeada($"CONTRATO_ITEM_INEXISTENTE: item {itemId} nao pertence ao contrato ou ao tenant.");
public sealed class RecebimentoIntegradoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"RECEBIMENTO_INTEGRADO_INEXISTENTE: recebimento {id} nao encontrado no tenant.");
public sealed class ObrigacaoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"OBRIGACAO_INEXISTENTE: obrigacao financeira {id} nao encontrada no tenant.");
public sealed class DestinoLocalInexistenteException(long localId) : ComprasIntegradasFalhaNomeada($"DESTINO_LOCAL_INEXISTENTE: local de almoxarifado {localId} inexistente no tenant.");
public sealed class DestinoMaterialInexistenteException(long materialId) : ComprasIntegradasFalhaNomeada($"DESTINO_MATERIAL_INEXISTENTE: material {materialId} inexistente no tenant.");
public sealed class RecebimentoRejeicaoSemMotivoException() : ComprasIntegradasFalhaNomeada($"RECEBIMENTO_REJEICAO_SEM_MOTIVO: rejeicao parcial do recebimento exige motivo registrado (BR-7.5).");
public sealed class RejecaoDesfechoInvalidoException(string desfecho) : ComprasIntegradasFalhaNomeada($"RECEBIMENTO_DESFECO_INVALIDO: desfecho deve ser SUBSTITUIDO ou ACATADO (recebido '{desfecho}').");
public sealed class FaturaNaturezaInvalidaException(string natureza) : ComprasIntegradasFalhaNomeada($"FATURA_NATUREZA_INVALIDA: natureza deve ser NFE ou NFS_E (recebido '{natureza}').");
public sealed class FaturaFornecedorDiscrepanteException(string cnpjApresentado, string cnpjContratado) : ComprasIntegradasFalhaNomeada($"FATURA_FORNECEDOR_DISCREPANTE: fatura emitida por CNPJ {cnpjApresentado} diverge do contratado {cnpjContratado} (conferencia BR-7.5).");
public sealed class FaturaValorDivergenteException(decimal valorFatura, decimal valorPedido) : ComprasIntegradasFalhaNomeada($"FATURA_VALOR_DIVERGENTE_DO_PEDIDO: valor da fatura {valorFatura:F2} diverge do valor do fornecimento/pedido {valorPedido:F2} (conferencia BR-7.5).");
public sealed class FornecimentoExcedeItemContratoException(long contratoItemId, decimal quantidade, decimal disponivel) : ComprasIntegradasFalhaNomeada($"FONTECIMENTO_EXCEDE_ITEM_CONTRATO: quantidade {quantidade:F3} excede o disponivel {disponivel:F3} do item de contrato {contratoItemId}.");
public sealed class FornecimentoChaveIdempotenciaConflitoException(string chave, long fornecimentoId) : ComprasIntegradasFalhaNomeada($"FORNECIMENTO_CHAVE_IDEMPOTENCIA_CONFLITO: chave '{chave}' ja registrada no fornecimento {fornecimentoId} com conteudo diferente.");
public sealed class FornecimentoEncerradoParaRecebimentoException(long id) : ComprasIntegradasFalhaNomeada($"FORNECIMENTO_ENCERRADO_PARA_RECEBIMENTO: fornecimento {id} ja totalmente recebido - recebimento extra bloqueado.");
public sealed class RecebimentoAcimaFornecidoException(decimal recebidoTotal, decimal fornecido) : ComprasIntegradasFalhaNomeada($"RECEBIMENTO_ACIMA_FORNECIDO: aprovado+rejeitado acumulado {recebidoTotal:F3} excede o fornecido {fornecido:F3} (BR-7.5).");
public sealed class RejecaoDesfechoJaRegistradoException(long recebimentoId) : ComprasIntegradasFalhaNomeada($"REJEICAO_DESFECO_JA_REGISTRADO: rejeicao do recebimento {recebimentoId} ja possui desfecho registrado.");
public sealed class FaturaJaVinculadaConflitoException(long fornecimentoId, long faturaId, string numeroExistente) : ComprasIntegradasFalhaNomeada($"FATURA_JA_VINCULADA_CONFLITO: fornecimento {fornecimentoId} ja tem a fatura {faturaId} (numero {numeroExistente}) vinculada com conteudo diferente.");
public sealed class DestinoExcedeQtdAprovadaException(decimal destinoNovo, decimal disponivel) : ComprasIntegradasFalhaNomeada($"DESTINO_EXCEDE_QTD_APROVADA: destino {destinoNovo:F3} excede a quantidade aprovada ainda destinavel {disponivel:F3} (BR-7.6).");
public sealed class DestinoReferenciaConflitoException(string referencia, long destinoId) : ComprasIntegradasFalhaNomeada($"DESTINO_REFERENCIA_CONFLITO: referencia '{referencia}' ja registrada no destino {destinoId} com conteudo diferente (movimento referenciado unico).");
public sealed class DestinoLocalInativoException(long localId) : ComprasIntegradasFalhaNomeada($"DESTINO_LOCAL_INATIVO: local de almoxarifado {localId} inexistente ou inativo (BR-7.6).");
public sealed class DestinoTipoInvalidoException(string alvoTipo) : ComprasIntegradasFalhaNomeada($"DESTINO_TIPO_INVALIDO: alvo deve ser ESTOQUE ou PATRIMONIO (recebido '{alvoTipo}').");
public sealed class ObrigacaoFornecimentoIncompletoException(long fornecimentoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"OBRIGACAO_FORNECIMENTO_INCOMPLETO: fornecimento {fornecimentoId} em '{statusAtual}' - obrigacao financeira so nasce de fornecimento CONCLUIDO.");
public sealed class ObrigacaoDuplicadaReferenciaException(string referencia, long obrigacaoId) : ComprasIntegradasFalhaNomeada($"OBRIGACAO_DUPLICADA_REFERENCIA: referencia '{referencia}' ja consumida pela obrigacao {obrigacaoId} (1 fornecimento aprovado = 1 obrigacao; reprocesso com conteudo divergente conflita).");
public sealed class ObrigacaoLiquidacaoSemFaturaException(long obrigacaoId) : ComprasIntegradasFalhaNomeada($"OBRIGACAO_LIQUIDACAO_SEM_FATURA: obrigacao {obrigacaoId} nao liquida sem fatura/NF-e vinculada e conferida ao fornecimento.");
public sealed class ObrigacaoJaLiquidadaException(long obrigacaoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"OBRIGACAO_JA_LIQUIDADA: obrigacao {obrigacaoId} em estado terminal '{statusAtual}' nao aceita liquidacao.");
public sealed class ObrigacaoEstornoSemJustificativaException() : ComprasIntegradasFalhaNomeada($"OBRIGACAO_ESTORNO_SEM_JUSTIFICATIVA: estorno por compensacao exige justificativa (sem delete fisico).");
// Sprint 10 (BR-7.8 + BR-10.x) - 404 - recursos ausentes nos modulos vinculados
public sealed class ProcessoComprasInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"PROCESSO_COMPRAS_INEXISTENTE: processo de contratacao {id} nao encontrado no tenant.");
public sealed class ConsultaJuridicaInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"CONSULTA_JURIDICA_INEXISTENTE: consulta juridica {id} nao encontrada no tenant.");
public sealed class CondicaoParecerInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"CONDICAO_PARECER_INEXISTENTE: condicao de parecer {id} nao encontrada no tenant.");
public sealed class ProcessoJuridicoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"PROCESSO_JURIDICO_INEXISTENTE: contencioso (juridico_processo) {id} nao encontrado no tenant.");
public sealed class InstrumentoConvenioInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"INSTRUMENTO_CONVENIO_INEXISTENTE: instrumento de convenio {id} nao encontrado no tenant.");
public sealed class ObraInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"OBRA_INEXISTENTE: obra {id} nao encontrada no tenant.");
public sealed class MedicaoObrasInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"MEDICAO_OBRAS_INEXISTENTE: medicao de obra {id} nao encontrada no tenant.");
public sealed class VinculoExternoInexistenteException(long id) : ComprasIntegradasFalhaNomeada($"VINCULO_EXTERNO_INEXISTENTE: vinculo externo {id} nao encontrado no tenant.");

// Sprint 10 (BR-7.8 + BR-10.x) - 400 - falhas de entrada/regra simples
public sealed class ConsultaTipoInvalidoException(string tipo) : ComprasIntegradasFalhaNomeada($"CONSULTA_TIPO_INVALIDO: tipo deve ser PARECER/IMPUGNACAO/DEFESA/COBRANCA (recebido '{tipo}').");
public sealed class ParecerConclusaoInvalidaException(string conclusao) : ComprasIntegradasFalhaNomeada($"PARECER_CONCLUSAO_INVALIDA: conclusao deve ser FAVORECIMENTO/IMPROCEDENCIA/CONDICIONADO (recebido '{conclusao}').");
public sealed class ParecerCondicionadoSemCondicaoException() : ComprasIntegradasFalhaNomeada($"PARECER_CONDICIONADO_SEM_CONDICAO: parecer com conclusao CONDICIONADO exige ao menos 1 condicionante registrada (BR-10.1).");
public sealed class ParecerCondicaoIndevidaException() : ComprasIntegradasFalhaNomeada($"PARECER_CONDICAO_INDEVIDA: condicionantes somente para parecer CONDICIONADO; FAVORECIMENTO/IMPROCEDENCIA nao admite condicoes (BR-10.1).");
public sealed class SatisfazerCondicaoSemJustificativaException() : ComprasIntegradasFalhaNomeada($"SATISFAZER_CONDICAO_SEM_JUSTIFICATIVA: satisfazer condicao de parecer condicionado exige justificativa registrada (BR-10.1).");
public sealed class ConsultaSemProcessoException(long id) : ComprasIntegradasFalhaNomeada($"CONSULTA_SEM_PROCESSO: consulta juridica {id} sem vinculo de contratacao - filial (recurso) nao tem processo a herdar.");
public sealed class ContenciosoOrigemInvalidaException(string origem) : ComprasIntegradasFalhaNomeada($"CONTENCIOSO_ORIGEM_INVALIDA: origem deve ser CONTRATO/CONVENIO/TRABALHISTA/TRIBUTARIO/OUTRA (recebido '{origem}').");
public sealed class CasoContenciosoInvalidoException(string caso) : ComprasIntegradasFalhaNomeada($"CASO_CONTENCIOSO_INVALIDO: caso deve ser ATIVO ou PASSIVO (recebido '{caso}').");
public sealed class ContenciosoResultadoInvalidoException(string resultado) : ComprasIntegradasFalhaNomeada($"CONTENCIOSO_RESULTADO_INVALIDO: resultado deve ser TRANSACAO/JUGADO/DESISTENCIA (recebido '{resultado}').");
public sealed class DesfechoSemJustificativaException() : ComprasIntegradasFalhaNomeada($"DESFECO_SEM_JUSTIFICATIVA: desfecho de contencioso exige justificativa registrada (BR-10.2).");
public sealed class QuitarVinculoSemJustificativaException() : ComprasIntegradasFalhaNomeada($"QUITAR_VINCULO_SEM_JUSTIFICATIVA: quebra de vinculo exige justificativa e ato registrado (BR-10.5).");
public sealed class ContrapartidaTipoInvalidoException(string tipo) : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_TIPO_INVALIDO: tipo deve ser FINANCEIRA/BENS/SERVICOS/PESSOAL/OUTRA (recebido '{tipo}').");
public sealed class ContrapartidaValorInconsistenteException(decimal previsto, decimal realizado) : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_VALOR_INCONSISTENTE: previsto {previsto:F2}/realizado {realizado:F2} incoerentes (previsto > 0 e 0 <= realizado <= previsto, BR-7.8).");
public sealed class ContrapartidaRealizacaoSemDataException() : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_REALIZACAO_SEM_DATA: contrapartida REALIZADA/PARCIAL exige dataRealizacao registrada (BR-7.8).");
public sealed class InstrumentoEmRascunhoException(long id) : ComprasIntegradasFalhaNomeada($"INSTRUMENTO_EM_RASCUNHO: instrumento de convenio {id} em RASCUNHO nao aceita atos operacionais.");
public sealed class EncerrarConvenioSaldoSemJustificativaException(decimal saldo) : ComprasIntegradasFalhaNomeada($"ENCERRAR_CONVENIO_SALDO_SEM_JUSTIFICATIVA: encerramento com saldo de execucao nao justificado {saldo:F2} (repasse recebido menos etapas executadas) exige justificativa (BR-10.3).");
public sealed class PrestacaoTipoInvalidoException(string tipo) : ComprasIntegradasFalhaNomeada($"PRESTACAO_TIPO_INVALIDO: tipo deve ser PARCIAL ou FINAL (recebido '{tipo}').");
public sealed class MedicaoStatusInvalidoException(long id, string statusAtual) : ComprasIntegradasFalhaNomeada($"MEDICAO_STATUS_INVALIDO: medicao de obra {id} em '{statusAtual}' nao aceita homologacao (estado esperado ENVIADA, BR-10.4).");

// Sprint 10 (BR-7.8 + BR-10.x) - 409 - gates da frente + conflitos de estado
public sealed class ParecerCondicaoAbertaException(long processoId, int abertas) : ComprasIntegradasFalhaNomeada($"PARECER_CONDICAO_ABERTA: processo de contratacao {processoId} tem parecer condicionado com {abertas} condicao(oes) aberta(s) - proximo passo da contratacao bloqueado ate satisfacao registrada (BR-10.1).");
public sealed class ProrrogacaoBloqueadaContenciosoAtivoException(long contratoId, int ativos) : ComprasIntegradasFalhaNomeada($"PRORROGACAO_BLOQUEADA_CONTENCIOSO_ATIVO: contrato {contratoId} possui contencioso ativo ({ativos}) - prorrogação bloqueada ate desfecho registrado (BR-10.2).");
public sealed class ObraContratoSemProjetoException(long obraId) : ComprasIntegradasFalhaNomeada($"OBRA_CONTRATO_SEM_PROJETO: contrato de obra {obraId} sem projeto APROVADO (obras_projeto) nao se gera - licitacao de projeto basico previa (BR-10.4, Lei 14.133 art. 185).");
public sealed class ContrapartidaConvenioInexistenteException(long instrumentoId, decimal necessario, decimal realizado) : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_CONVENIO_INEXISTENTE: instrumento de convenio {instrumentoId} exige contrapartida {necessario:F2} e apenas {realizado:F2} foi realizado - aplicacao de recurso do convenio a contrato bloqueada (BR-7.8).");
public sealed class ObraMedicaoInsuficienteException(double pctFinanceiro, double pctFisico) : ComprasIntegradasFalhaNomeada($"OBRA_MEDICAO_INSUFICIENTE: pagamento financeiro acumulado em {pctFinanceiro:F2}% excede o medido fisicamente ({pctFisico:F2}%) - homologacao da medicao bloqueada (BR-10.4).");
public sealed class ConsultaChaveIdempotenciaConflitoException(string chave, long consultaId) : ComprasIntegradasFalhaNomeada($"CONSULTA_CHAVE_IDEMPOTENCIA_CONFLITO: chave '{chave}' ja registrada na consulta juridica {consultaId} com conteudo diferente.");
public sealed class FilialDuplicadaException(long paiId) : ComprasIntegradasFalhaNomeada($"FILIAL_DUPLOCADA: consulta original {paiId} ja possui filial (recurso) registrada - 1 filial por consulta por tenant.");
public sealed class ConsultaPaiNaoResolvidaException(long paiId, string statusAtual) : ComprasIntegradasFalhaNomeada($"CONSULTA_PAI_NAO_RESOLVIDA: consulta original {paiId} em '{statusAtual}' - recurso/filial so abre contra decisao RESOLVIDA (original preservada, BR-10.1).");
public sealed class ParecerJaEmitidoException(long consultaId) : ComprasIntegradasFalhaNomeada($"PARECER_JA_EMITIDO: consulta juridica {consultaId} ja possui parecer EMITIDO - nova conclusao por novo ato.");
public sealed class CondicaoJaSatisfeitaException(long id) : ComprasIntegradasFalhaNomeada($"CONDICAO_JA_SATISFEITA: condicao {id} ja satisfeita - ato registrado uma unica vez.");
public sealed class ContenciosoAtivoParaOrigemException(string origemTipo, long origemId) : ComprasIntegradasFalhaNomeada($"CONTENCIOSO_ATIVO_PARA_ORIGEM: origem {origemTipo}/{origemId} ja possui contencioso ativo - 1 contencioso por origem ate desfecho registrado (BR-10.2).");
public sealed class ContenciosoJaDesfecadoException(long id, string resultado) : ComprasIntegradasFalhaNomeada($"CONTENCIOSO_JA_DESFECADO: contencioso {id} ja desfeito com resultado '{resultado}' - movimentos sao append-only.");
public sealed class ContrapartidaChaveIdempotenciaConflitoException(string chave, long contrapartidaId) : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_CHAVE_IDEMPOTENCIA_CONFLITO: chave '{chave}' ja registrada na contrapartida {contrapartidaId} com conteudo diferente.");
public sealed class ContrapartidaExcedeInstrumentoException(decimal novoPrevisto, decimal disponivel) : ComprasIntegradasFalhaNomeada($"CONTRAPARTIDA_EXCEDE_INSTRUMENTO: prevista nova {novoPrevisto:F2} faz o total de contrapartidas previstas exceder o teto do instrumento (disponivel ainda {disponivel:F2}).");
public sealed class InstrumentoEncerradoImutavelException(long id, string statusAtual) : ComprasIntegradasFalhaNomeada($"INSTRUMENTO_ENCERRADO_IMUTAVEL: instrumento de convenio {id} em estado terminal '{statusAtual}' nao aceita novas contrapartidas.");
public sealed class InstrumentoNaoAtivoException(long id, string statusAtual) : ComprasIntegradasFalhaNomeada($"INSTRUMENTO_NAO_ATIVO: instrumento de convenio {id} em '{statusAtual}' nao aceita a operacao (estado esperado ATIVO).");
public sealed class VinculoConvenioChaveConflitoException(string chave, long vinculoId) : ComprasIntegradasFalhaNomeada($"VINCULO_CONVENIO_CHAVE_CONFLITO: chave '{chave}' ja registrada no vinculo {vinculoId} com conteudo diferente.");
public sealed class ConvenioEncerramentoBloqueadoContratoAtivoException(long instrumentoId, int contratosAtivos) : ComprasIntegradasFalhaNomeada($"CONVENIO_ENCERRAMENTO_BLOQUEADO_CONTRATO_ATIVO: instrumento de convenio {instrumentoId} possui {contratosAtivos} contrato(s) vinculado(s) ativo(s) - encerramento bloqueado pela cascata (BR-10.5).");
public sealed class InstrumentoJaEncerradoException(long id, string statusAtual) : ComprasIntegradasFalhaNomeada($"INSTRUMENTO_JA_ENCERRADO: instrumento de convenio {id} em estado terminal '{statusAtual}' nao aceita novo encerramento.");
public sealed class PrestacaoExecucaoMinimaNaoAtingidaException(double pctExecucao, int minimoPct) : ComprasIntegradasFalhaNomeada($"PRESTACAO_EXECUCAO_MINIMA_NAO_ATINGIDA: execucao {pctExecucao:F2}% abaixo do minimo parametrico {minimoPct}% dos repasses recebidos (CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT) - prestacao de contas bloqueada (BR-10.3).");
public sealed class PrestacaoNumeroDuplicadoException(long instrumentoId, string numero) : ComprasIntegradasFalhaNomeada($"PRESTACAO_NUMERO_DUPLOCADO: instrumento de convenio {instrumentoId} ja possui prestacao de contas numero '{numero}'.");
public sealed class PrestacaoFinalSaldoNaoJustificadoException(long instrumentoId, decimal saldo) : ComprasIntegradasFalhaNomeada($"PRESTACAO_FINAL_SALDO_NAO_JUSTIFICADO: prestacao FINAL do instrumento {instrumentoId} com saldo de execucao {saldo:F2} (repasse recebido menos etapas executadas) - saldo deve ser zerado antes da final (BR-10.3).");
public sealed class VinculoJaQuitadoException(long id) : ComprasIntegradasFalhaNomeada($"VINCULO_JA_QUITADO: vinculo externo {id} ja QUITADO - quebra registrada uma unica vez com justificativa (BR-10.5).");
public sealed class MedicaoJaHomologadaException(long id) : ComprasIntegradasFalhaNomeada($"MEDICAO_JA_HOMOLOGADA: medicao de obra {id} ja homologada.");
public sealed class ContratoNaoVigenteParaVinculoException(long contratoId, string statusAtual) : ComprasIntegradasFalhaNomeada($"CONTRATO_NAO_VIGENTE_PARA_VINCULO: contrato {contratoId} em '{statusAtual}' nao recebe vinculo de recurso de convenio (esperado VIGENTE/PRORROGADO).");

// ------------------------------- servico ----------------------------------
public interface IComprasIntegradasService
{
    Task<IReadOnlyList<DemandaIntegradaDto>> DemandasAsync(ComprasIntegradasContext x, CancellationToken ct);
    Task<DemandaIntegradaDto> DemandaAsync(ComprasIntegradasContext x, long id, CancellationToken ct);
    Task<long> CriarDemandaAsync(ComprasIntegradasContext x, DemandaCriarRequest r, CancellationToken ct);
    Task<DemandaIntegradaDto> DemandaAprovarAsync(ComprasIntegradasContext x, long id, CancellationToken ct);
    Task<DemandaIntegradaDto> DemandaRepelirAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct);
    Task<DemandaIntegradaDto> DemandaReabrirAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct);
    Task<DemandaIntegradaDto> DemandaCancelarAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct);

    Task<ProcedimentoAbertoDto> ProcedimentoAbrirAsync(ComprasIntegradasContext x, ProcedimentoAbrirRequest r, CancellationToken ct);
    Task HabilitacaoJulgarAsync(ComprasIntegradasContext x, long licitacaoId, HabilitacaoJulgarRequest r, CancellationToken ct);
    Task<long> LanceRegistrarAsync(ComprasIntegradasContext x, long licitacaoId, LanceRegistrarRequest r, CancellationToken ct);
    Task<PreferenciaDesfechoDto> PreferenciaDocumentarAsync(ComprasIntegradasContext x, long licitacaoId, PreferenciaDocumentarRequest r, CancellationToken ct);
    Task<ClassificacaoDto> JulgamentoExecutarAsync(ComprasIntegradasContext x, long licitacaoId, string? justificativaEmpate, CancellationToken ct);
    Task<AdjudicacaoDto> AdjudicarAsync(ComprasIntegradasContext x, long licitacaoId, AdjudicarRequest r, CancellationToken ct);
    Task<long> HomologarAsync(ComprasIntegradasContext x, long licitacaoId, CancellationToken ct);
    Task<PropostaIntegradaDto> PropostaRejeitarAsync(ComprasIntegradasContext x, long licitacaoId, long propostaId, string motivo, CancellationToken ct);

    Task<ContratoGeradoDto> ContratoGerarAsync(ComprasIntegradasContext x, ContratoGerarRequest r, CancellationToken ct);
    Task<ContratoExecucaoDto> ContratoExecucaoAsync(ComprasIntegradasContext x, long contratoId, CancellationToken ct);
    Task ContratoProrrogarAsync(ComprasIntegradasContext x, long contratoId, DateOnly novoFim, string justificativa, CancellationToken ct);
    Task ContratoRescindirAsync(ComprasIntegradasContext x, long contratoId, string tipo, string justificativa, string? notificacaoRef, CancellationToken ct);

    // Sprint 9 (BR-7.5 a BR-7.7): fornecimento -> recebimento -> fatura -> destinacao -> obrigacao
    Task<long> FornecimentoCriarAsync(ComprasIntegradasContext x, FornecimentoCriarRequest r, CancellationToken ct);
    Task<FornecimentoIntegradoDto> FornecimentoAsync(ComprasIntegradasContext x, long id, CancellationToken ct);
    Task<RecebimentoIntegradoDto> RecebimentoIntegradoRegistrarAsync(ComprasIntegradasContext x, long fornecimentoId, RecebimentoIntegradoRegistrarRequest r, CancellationToken ct);
    Task<RecebimentoIntegradoDto> RecebimentoDesfechoAsync(ComprasIntegradasContext x, long recebimentoId, RecebimentoDesfechoRequest r, CancellationToken ct);
    Task<FaturaIntegradaDto> FaturaVincularAsync(ComprasIntegradasContext x, long fornecimentoId, FaturaVincularRequest r, CancellationToken ct);
    Task<DestinacaoIntegradaDto> DestinacaoRegistrarAsync(ComprasIntegradasContext x, DestinacaoIntegradaRequest r, CancellationToken ct);
    Task<ObrigacaoFinanceiraDto> ObrigacaoGerarAsync(ComprasIntegradasContext x, ObrigacaoGerarRequest r, CancellationToken ct);
    Task<ObrigacaoFinanceiraDto> ObrigacaoLiquidarAsync(ComprasIntegradasContext x, long obrigacaoId, CancellationToken ct);
    Task<ObrigacaoFinanceiraDto> ObrigacaoEstornarAsync(ComprasIntegradasContext x, long obrigacaoId, string justificativa, CancellationToken ct);

    // Sprint 10 (BR-7.8 + BR-10.x): vinculos juridico + convenios/obras (etapa 3/3)
    Task<ConsultaJuridicaDto> ConsultaJuridicaCriarAsync(ComprasIntegradasContext x, ConsultaJuridicaCriarRequest r, CancellationToken ct);
    Task<ConsultaJuridicaDto> ParecerEmitirAsync(ComprasIntegradasContext x, long consultaId, ParecerEmitirRequest r, CancellationToken ct);
    Task<CondicaoSatisfeitaDto> CondicaoSatisfazerAsync(ComprasIntegradasContext x, long condicaoId, string justificativa, CancellationToken ct);
    Task<ConsultaJuridicaDto> ConsultaFilialAsync(ComprasIntegradasContext x, long consultaId, string justificativa, CancellationToken ct);
    Task<ContenciosoDto> ContenciosoAbrirAsync(ComprasIntegradasContext x, ContenciosoAbrirRequest r, CancellationToken ct);
    Task<ContenciosoDto> ContenciosoDesfecharAsync(ComprasIntegradasContext x, long processoId, ContenciosoDesfecharRequest r, CancellationToken ct);
    Task<ContrapartidaDto> ContrapartidaRegistrarAsync(ComprasIntegradasContext x, ContrapartidaRegistrarRequest r, CancellationToken ct);
    Task<VinculoExternoDto> VinculoConvenioAplicarAsync(ComprasIntegradasContext x, VinculoConvenioAplicarRequest r, CancellationToken ct);
    Task<ConvencioEncerradoDto> ConvencioEncerrarAsync(ComprasIntegradasContext x, long instrumentoId, string? justificativa, CancellationToken ct);
    Task<PrestacaoDto> PrestacaoRegistrarAsync(ComprasIntegradasContext x, PrestacaoRegistrarRequest r, CancellationToken ct);
    Task<VinculoExternoDto> VinculoQuitarAsync(ComprasIntegradasContext x, long vinculoId, string justificativa, CancellationToken ct);
    Task<MedicaoHomologadaDto> MedicaoHomologarAsync(ComprasIntegradasContext x, long medicaoId, CancellationToken ct);
    Task<IReadOnlyList<VinculoExternoDto>> VinculosContratoAsync(ComprasIntegradasContext x, long contratoId, CancellationToken ct);
}
