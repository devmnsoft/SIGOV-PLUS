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
public sealed record ContratoGerarRequest(long HomologacaoId, DateOnly DataEficacia, DateOnly DataFim, string AssinaturaModo, string? Gestor = null, string? Fiscal = null, string? Numero = null);

// --------------------------------- dtos -----------------------------------
public sealed record DemandaIntegradaDto(long Id, string Objeto, string Status, bool Urgencia, decimal ValorEstimado, int NItens, long? CentroCustoId, long? ProcessoId, long CriadoPor, long? AprovadoPor, long? DuplaAprovadoPor, DateTimeOffset CriadoEm);
public sealed record ProcedimentoAbertoDto(long ProcessoId, long LicitacaoId, string Modalidade, string Criterio, bool JaExiste);
public sealed record PreferenciaDesfechoDto(string ZonaStatus, string Desfecho, bool DentroDaMargem, decimal MelhorLanceMeEpp, decimal? MelhorLanceForaMeEpp, int MargemPct);
public sealed record ClassificacaoItemDto(int Ordem, long FornecedorId, decimal Valor, string Status);
public sealed record ClassificacaoDto(IReadOnlyList<ClassificacaoItemDto> Itens, string? JustificativaEmpate);
public sealed record AdjudicacaoDto(long AdjudicacaoId, long PropostaVencedoraId, long FornecedorId, decimal ValorTotal, int NItens);
public sealed record PropostaIntegradaDto(long Id, long FornecedorId, decimal Valor, int? OrdemClassificacao, string Status);
public sealed record ContratoGeradoDto(long ContratoId, string Numero, long FornecedorId, decimal ValorContratado, int NItens, DateOnly DataEficacia, DateOnly DataFim, string AssinaturaModo, string Status);
public sealed record ContratoExecucaoDto(long ContratoId, decimal ValorContratado, decimal TotalFornecido, decimal PctExecutado, int TetoPct, bool BloqueiaNovoFornecimento);

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
}
