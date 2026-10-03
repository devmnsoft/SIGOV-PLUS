using Sigov.Application.Common;

namespace Sigov.Application.ComprasEmpresariais;

public sealed record ComprasContext(Guid TenantId, Guid UsuarioId, string CorrelationId, bool SomenteLeitura = false);
public sealed record FornecedorFiltro(string? Busca = null, string? Status = null, int Pagina = 1, int Tamanho = 20);
public sealed record FornecedorResumo(Guid Id, string Codigo, string RazaoSocial, string? NomeFantasia, string DocumentoMascarado, string Status, decimal Score, long Version);
public sealed record CriarFornecedorRequest(string TipoPessoa, string Documento, string RazaoSocial, string? NomeFantasia, string? Categoria, string? Porte, string? CondicaoPagamento, int PrazoMedio, string? Observacoes);
public sealed record AlterarStatusRequest(string Status, string? Motivo, long Version);
public sealed record AdicionarContatoRequest(string Nome, string? Email, string? Telefone, bool Principal);
public sealed record AdicionarEnderecoRequest(string Tipo, string Logradouro, string? Numero, string? Complemento, string? Bairro, string Cidade, string Uf, string? Cep);
public sealed record AdicionarDocumentoRequest(Guid DocumentoGedId, string Tipo, bool Obrigatorio, DateOnly? Validade);
public sealed record RequisicaoItemRequest(string Tipo, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, decimal ValorEstimado, bool PermiteParcial, bool ExigeInspecao);
public sealed record CriarRequisicaoRequest(string? Setor, Guid? CentroCustoId, Guid? ProjetoId, Guid? ContratoId, Guid? OrdemServicoId, Guid? AlmoxarifadoId, string Urgencia, DateOnly? DataNecessaria, string Justificativa, string? Observacoes, IReadOnlyList<RequisicaoItemRequest> Itens);
public sealed record AtualizarRequisicaoRequest(string? Setor, Guid? CentroCustoId, Guid? ProjetoId, Guid? ContratoId, Guid? OrdemServicoId, Guid? AlmoxarifadoId, string Urgencia, DateOnly? DataNecessaria, string Justificativa, string? Observacoes, IReadOnlyList<RequisicaoItemRequest> Itens, long Version);
public sealed record RequisicaoFiltro(
 string? Busca = null,
 string? Status = null,
 DateOnly? DataInicial = null,
 DateOnly? DataFinal = null,
 int Pagina = 1,
 int Tamanho = 20,
 string OrdenarPor = "data",
 string Direcao = "desc");
public sealed record RequisicaoResumo(Guid Id, string Numero, string Status, decimal ValorEstimado, DateOnly? DataNecessaria, DateTime DataSolicitacao, string Origem, int Itens, long Version);
public sealed record RequisicaoItemDetalhe(Guid Id, int Ordem, string Tipo, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, decimal ValorEstimado, bool PermiteParcial, bool ExigeInspecao);
public sealed record RequisicaoHistorico(string Acao, string? Detalhes, DateTime CriadoEm);
public sealed record RequisicaoDetalhe(Guid Id, string Numero, string Status, string? Setor, string Urgencia, DateOnly? DataNecessaria, string Justificativa, string? Observacoes, decimal ValorEstimado, long Version, IReadOnlyList<RequisicaoItemDetalhe> Itens, IReadOnlyList<RequisicaoHistorico> Historico);
public sealed record ComprasDashboard(decimal TotalSolicitado, decimal ValorAprovado, int AprovacoesPendentes, int CotacoesAbertas, int PedidosAtrasados, int RecebimentosPendentes, int FaturasBloqueadas, int DocumentosVencendo);
public sealed record RecebimentoFiltro(string? Fornecedor = null, string? Pedido = null, DateOnly? DataInicial = null, DateOnly? DataFinal = null, string? Status = null, Guid? AlmoxarifadoId = null, string? Responsavel = null, int Pagina = 1, int Tamanho = 20);
public sealed record RecebimentoResumo(Guid Id, Guid PedidoId, string PedidoNumero, string FornecedorNome, string Status, string Documento, string AlmoxarifadoNome, DateTime CriadoEm, string Responsavel, int Divergencias);
public sealed record RecebimentoTotais(long Aptos, long EmConferencia, long Concluidos, long Divergencias);
public sealed record CentralRecebimentos(Common.PagedResult<RecebimentoResumo> Resultado, RecebimentoTotais Totais);
public sealed record PedidoRecebimentoItem(long Id, Guid ProdutoId, string Produto, string Unidade, decimal QuantidadePedida, decimal QuantidadeCancelada, decimal QuantidadeFisica, decimal QuantidadeAceita, decimal QuantidadeRejeitada, decimal QuantidadeEmConferencia, decimal QuantidadePendente, bool ExigeInspecao);
public sealed record AlmoxarifadoOpcao(Guid Id,string Nome);
public sealed record PedidoParaRecebimento(Guid Id, string Numero, string Status, Guid FornecedorId, string Fornecedor, long Version, IReadOnlyList<PedidoRecebimentoItem> Itens,IReadOnlyList<AlmoxarifadoOpcao> Almoxarifados);
public sealed record RecebimentoItemRequest(long PedidoItemId, decimal Quantidade, string? Lote, DateOnly? Validade, string? NumeroSerie, long? DivergenciaOrigemId = null);
public sealed record CriarRecebimentoRequest(Guid PedidoId, Guid AlmoxarifadoId, string Documento, DateTimeOffset DataOperacao, string? Observacoes, IReadOnlyList<RecebimentoItemRequest> Itens, long PedidoVersion);
public sealed record RecebimentoCriado(Guid Id, string Status, bool Repetido);
public sealed record RecebimentoItemDetalhe(long Id, string Produto, string Unidade, decimal QuantidadeFisica, decimal QuantidadeAceita, decimal QuantidadeRejeitada, decimal QuantidadeConferencia, string? Lote, DateOnly? Validade, string? NumeroSerie, long? DivergenciaOrigemId = null);
public sealed record RecebimentoEvento(string Tipo, string? Detalhes, DateTime OcorridoEm);
public sealed record RecebimentoDetalhe(Guid Id, Guid PedidoId, string PedidoNumero, string Fornecedor, string Documento, string Almoxarifado, DateTimeOffset DataOperacao, string Status, string ResultadoInspecao, string? Observacoes, long Version, IReadOnlyList<RecebimentoItemDetalhe> Itens, IReadOnlyList<RecebimentoEvento> Historico);
public sealed record InspecaoItemRequest(long RecebimentoItemId, decimal QuantidadeAceita, decimal QuantidadeRejeitada);
public sealed record ConcluirInspecaoRequest(long Version, string? Justificativa, IReadOnlyList<InspecaoItemRequest> Itens);
public sealed record DivergenciaFiltro(Guid? RecebimentoId=null,string? Fornecedor=null,string? Produto=null,string? Situacao=null,Guid? ResponsavelId=null,bool NaoAtribuidas=false,DateOnly? AberturaInicial=null,DateOnly? AberturaFinal=null,DateOnly? EncerramentoInicial=null,DateOnly? EncerramentoFinal=null,int Pagina=1,int Tamanho=20);
public sealed record DivergenciaResumo(long Id,Guid RecebimentoId,string Documento,string PedidoNumero,string Fornecedor,string Produto,string Unidade,decimal QuantidadeRejeitada,string Motivo,string Situacao,Guid? ResponsavelId,string? ResponsavelNome,DateTime AbertaEm,DateTime? EncerradaEm,string? Providencia,string? Resultado,long Version);
public sealed record DivergenciaTotais(long Total,long Abertas,long EmTratamento,long Encerradas,long NaoAtribuidas);
public sealed record CentralDivergencias(PagedResult<DivergenciaResumo> Resultado,DivergenciaTotais Totais);
public sealed record DivergenciaEvento(long Id,string Tipo,string? Descricao,string? Providencia,string? Resultado,string? Justificativa,string? ResponsavelAnterior,string? ResponsavelNovo,Guid UsuarioId,string Autor,DateTime OcorridoEm,string? CorrelationId);
public sealed record DivergenciaDevolucaoVinculada(long DevolucaoId,string Situacao,decimal Quantidade,DateTime CriadaEm,DateTime? ExpedidaEm,DateTime? EntregueEm,string? DocumentoProtocolo,string? RecebedorOuReferencia);
public sealed record DestinacaoRejeitadoItem(long RecebimentoItemId,string Produto,string Unidade,decimal QuantidadeRejeitada,decimal QuantidadeReservada,decimal QuantidadeExpedidaNaoEntregue,decimal QuantidadeEntregue,decimal SaldoSemDestinacao,bool Inconsistente=false,string? DiagnosticoInconsistencia=null);
public sealed record DivergenciaDetalhe(long Id,Guid RecebimentoId,long RecebimentoItemId,string Documento,string PedidoNumero,string Fornecedor,string Produto,string Unidade,decimal QuantidadeRejeitada,string Motivo,string Situacao,Guid? ResponsavelId,string? ResponsavelNome,string? Providencia,string? Resultado,string? JustificativaEncerramento,DateTimeOffset AbertaEm,DateTimeOffset? EncerradaEm,long Version,IReadOnlyList<DivergenciaEvento> Historico,IReadOnlyList<DivergenciaDevolucaoVinculada>? DevolucoesVinculadas=null,DestinacaoRejeitadoItem? Destinacao=null,bool ReposicaoAutorizada=false,decimal QuantidadeReposicao=0m,decimal ReposicaoRecebida=0m,decimal SaldoReposicaoPendente=0m,string? ReposicaoJustificativa=null,long? DivergenciaOrigemId=null);
public sealed record ResponsavelDivergencia(Guid UsuarioId,string Nome,string? Vinculo,string? Unidade);
public sealed record AtribuirDivergenciaRequest(Guid ResponsavelId,long Version,string IdempotencyKey);
public sealed record RegistrarAndamentoDivergenciaRequest(string Descricao,string? Providencia,long Version,string IdempotencyKey);
public sealed record EncerrarDivergenciaRequest(string Resultado,string Justificativa,long Version,string IdempotencyKey);
public sealed record AutorizarReposicaoRequest(decimal Quantidade, string Justificativa, long Version, string IdempotencyKey);
public sealed record DivergenciaComandoResultado(long Id,string Situacao,long Version,bool Repetido,bool PendenciaConcluida);

public interface IFornecedorRepository
{
 Task<PagedResult<FornecedorResumo>> ListarAsync(Guid tenant, FornecedorFiltro filtro, CancellationToken ct);
 Task<FornecedorResumo?> ObterAsync(Guid tenant, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarFornecedorRequest request, string key, CancellationToken ct);
 Task AlterarStatusAsync(ComprasContext context, Guid id, AlterarStatusRequest request, CancellationToken ct);
 Task AdicionarContatoAsync(ComprasContext context, Guid id, AdicionarContatoRequest request, string key, CancellationToken ct);
 Task AdicionarEnderecoAsync(ComprasContext context, Guid id, AdicionarEnderecoRequest request, string key, CancellationToken ct);
 Task AdicionarDocumentoAsync(ComprasContext context, Guid id, AdicionarDocumentoRequest request, string key, CancellationToken ct);
}
public interface IDivergenciaRecebimentoRepository
{
 Task<CentralDivergencias> ListarAsync(Guid tenant,DivergenciaFiltro filtro,CancellationToken ct);
 Task<DivergenciaDetalhe?> ObterAsync(Guid tenant,long id,CancellationToken ct);
 Task<IReadOnlyList<ResponsavelDivergencia>> PesquisarResponsaveisAsync(Guid tenant,string? busca,int pagina,int tamanho,CancellationToken ct);
 Task<DivergenciaComandoResultado> AtribuirAsync(ComprasContext context,long id,AtribuirDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> RegistrarAndamentoAsync(ComprasContext context,long id,RegistrarAndamentoDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> EncerrarAsync(ComprasContext context,long id,EncerrarDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> AutorizarReposicaoAsync(ComprasContext context,long id,AutorizarReposicaoRequest request,CancellationToken ct);
}
public interface IDivergenciaRecebimentoApplicationService
{
 Task<CentralDivergencias> ListarAsync(ComprasContext context,DivergenciaFiltro filtro,CancellationToken ct);
 Task<DivergenciaDetalhe?> ObterAsync(ComprasContext context,long id,CancellationToken ct);
 Task<IReadOnlyList<ResponsavelDivergencia>> PesquisarResponsaveisAsync(ComprasContext context,string? busca,int pagina,int tamanho,CancellationToken ct);
 Task<DivergenciaComandoResultado> AtribuirAsync(ComprasContext context,long id,AtribuirDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> RegistrarAndamentoAsync(ComprasContext context,long id,RegistrarAndamentoDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> EncerrarAsync(ComprasContext context,long id,EncerrarDivergenciaRequest request,CancellationToken ct);
 Task<DivergenciaComandoResultado> AutorizarReposicaoAsync(ComprasContext context,long id,AutorizarReposicaoRequest request,CancellationToken ct);
}
public sealed class ComprasConcurrencyException(string message):InvalidOperationException(message);
public interface IRequisicaoCompraRepository
{
 Task<PagedResult<RequisicaoResumo>> ListarAsync(Guid tenant, RequisicaoFiltro filtro, CancellationToken ct);
 Task<RequisicaoDetalhe?> ObterAsync(Guid tenant, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarRequisicaoRequest request, string key, CancellationToken ct);
 Task AtualizarAsync(ComprasContext context, Guid id, AtualizarRequisicaoRequest request, CancellationToken ct);
 Task<RequisicaoEnvioResultado> EnviarAsync(ComprasContext context, Guid id, long version, string key, CancellationToken ct);
}
public interface IComprasDashboardRepository { Task<ComprasDashboard> ObterAsync(Guid tenant, CancellationToken ct); }
public interface IRecebimentoCompraRepository
{
 Task<CentralRecebimentos> ListarAsync(Guid tenant, RecebimentoFiltro filtro, CancellationToken ct);
 Task<PedidoParaRecebimento?> ObterPedidoAsync(Guid tenant, Guid pedidoId, CancellationToken ct);
 Task<RecebimentoDetalhe?> ObterAsync(Guid tenant, Guid id, CancellationToken ct);
 Task<RecebimentoCriado> CriarEConfirmarAsync(ComprasContext context, CriarRecebimentoRequest request, string key, CancellationToken ct);
 Task<RecebimentoCriado> ConcluirInspecaoAsync(ComprasContext context, Guid id, ConcluirInspecaoRequest request, CancellationToken ct);
}
public interface IFornecedorApplicationService
{
 Task<PagedResult<FornecedorResumo>> ListarAsync(ComprasContext context, FornecedorFiltro filtro, CancellationToken ct); Task<FornecedorResumo?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarFornecedorRequest request, string key, CancellationToken ct); Task AlterarStatusAsync(ComprasContext context, Guid id, AlterarStatusRequest request, CancellationToken ct);
 Task AdicionarContatoAsync(ComprasContext context, Guid id, AdicionarContatoRequest request, string key, CancellationToken ct); Task AdicionarEnderecoAsync(ComprasContext context, Guid id, AdicionarEnderecoRequest request, string key, CancellationToken ct); Task AdicionarDocumentoAsync(ComprasContext context, Guid id, AdicionarDocumentoRequest request, string key, CancellationToken ct);
}
public interface IRequisicaoCompraApplicationService { Task<PagedResult<RequisicaoResumo>> ListarAsync(ComprasContext context,RequisicaoFiltro filtro,CancellationToken ct); Task<RequisicaoDetalhe?> ObterAsync(ComprasContext context,Guid id,CancellationToken ct); Task<Guid> CriarAsync(ComprasContext context,CriarRequisicaoRequest request,string key,CancellationToken ct); Task AtualizarAsync(ComprasContext context,Guid id,AtualizarRequisicaoRequest request,CancellationToken ct); Task<RequisicaoEnvioResultado> EnviarAsync(ComprasContext context,Guid id,long version,string key,CancellationToken ct); }
public interface IComprasDashboardApplicationService { Task<ComprasDashboard> ObterAsync(ComprasContext context,CancellationToken ct); }
public interface IRecebimentoCompraApplicationService
{
 Task<CentralRecebimentos> ListarAsync(ComprasContext context, RecebimentoFiltro filtro, CancellationToken ct);
 Task<PedidoParaRecebimento?> ObterPedidoAsync(ComprasContext context, Guid pedidoId, CancellationToken ct);
 Task<RecebimentoDetalhe?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<RecebimentoCriado> CriarEConfirmarAsync(ComprasContext context, CriarRecebimentoRequest request, string key, CancellationToken ct);
 Task<RecebimentoCriado> ConcluirInspecaoAsync(ComprasContext context, Guid id, ConcluirInspecaoRequest request, CancellationToken ct);
}

public sealed record DevolucaoFiltro(string? Fornecedor=null,string? DocumentoRecebimento=null,string? Situacao=null,Guid? ResponsavelId=null,DateOnly? AberturaInicial=null,DateOnly? AberturaFinal=null,DateOnly? ExpedicaoInicial=null,DateOnly? ExpedicaoFinal=null,DateOnly? EntregaInicial=null,DateOnly? EntregaFinal=null,int Pagina=1,int Tamanho=20);
public sealed record DevolucaoResumo(long Id,Guid RecebimentoId,string DocumentoRecebimento,string Fornecedor,string Situacao,Guid ResponsavelId,string? ResponsavelNome,string OrigemFisica,string Destino,string Motivo,DateTime CriadaEm,DateTime? ExpedidaEm,DateTime? EntregueEm,long Version,int Itens);
public sealed record DevolucaoItemInput(long RecebimentoItemId,decimal Quantidade);
public sealed record CriarDevolucaoRequest(Guid RecebimentoId,string Motivo,Guid ResponsavelId,string OrigemFisica,string Destino,string EsferaGoverno,string TipoEntidade,string? OrgaoSuperior,string UnidadeGestora,string UnidadeExecutora,string HierarquiaAdministrativa,string AbrangenciaTerritorial,string? Uf,string? Municipio,string? Regiao,string? Jurisdicao,IReadOnlyList<DevolucaoItemInput> Itens,string IdempotencyKey);
public sealed record EditarDevolucaoRequest(string Motivo,Guid ResponsavelId,string OrigemFisica,string Destino,long Version,IReadOnlyList<DevolucaoItemInput> Itens,string IdempotencyKey);
public sealed record ExpedirDevolucaoRequest(long Version,DateTimeOffset SaidaEm,string Modalidade,string? Transportadora,string? ReferenciaTransporte,string IdempotencyKey);
public sealed record EntregarDevolucaoRequest(long Version,DateTimeOffset EntregueEm,string RecebedorOuReferencia,string DocumentoProtocolo,string? Observacao,string IdempotencyKey);
public sealed record CancelarDevolucaoRequest(long Version,string Justificativa,string IdempotencyKey);
public sealed record DevolucaoItemDetalhe(long Id,long RecebimentoItemId,string Produto,string Unidade,decimal QuantidadeRejeitada,decimal QuantidadeReservada,decimal QuantidadeExpedida,decimal QuantidadeEntregue,decimal SaldoElegivel);
public sealed record DevolucaoEvento(long Id,string Tipo,string? EstadoAnterior,string EstadoNovo,string Detalhes,Guid UsuarioId,DateTime OcorridoEm,string CorrelationId,string? AutorNome=null);
public sealed record DevolucaoDetalhe(long Id,Guid RecebimentoId,string DocumentoRecebimento,string PedidoNumero,Guid FornecedorId,string Fornecedor,string Situacao,Guid ResponsavelId,string? ResponsavelNome,string OrigemFisica,string Destino,string Motivo,string EsferaGoverno,string TipoEntidade,string UnidadeGestora,string UnidadeExecutora,string AbrangenciaTerritorial,long Version,DateTimeOffset CriadaEm,DateTimeOffset? ExpedidaEm,DateTimeOffset? EntregueEm,DateTimeOffset? CanceladaEm,string? Modalidade,string? ReferenciaTransporte,string? RecebedorOuReferencia,string? DocumentoProtocolo,IReadOnlyList<DevolucaoItemDetalhe> Itens,IReadOnlyList<DevolucaoEvento> Historico);
public sealed record OrigemDevolucao(Guid RecebimentoId,string Documento,string Fornecedor,IReadOnlyList<DevolucaoItemDetalhe> Itens,string? AlmoxarifadoNome=null);
public sealed record DevolucaoParaEdicao(long Id,Guid RecebimentoId,string DocumentoRecebimento,string Fornecedor,string Situacao,long Version,string Motivo,Guid ResponsavelId,string? ResponsavelNome,string OrigemFisica,string Destino,IReadOnlyList<DevolucaoItemDetalhe> Itens);
public sealed record OrigemElegivelResumo(Guid RecebimentoId,string Documento,string PedidoNumero,string Fornecedor,int ItensRejeitados,decimal SaldoTotalElegivel,DateTime ConcluidoEm);
public sealed record ContextoInstitucionalSnapshot(string EsferaGoverno,string TipoEntidade,string? OrgaoSuperior,string UnidadeGestora,string UnidadeExecutora,string HierarquiaAdministrativa,string AbrangenciaTerritorial,string? Uf,string? Municipio,string? Regiao,string? Jurisdicao);
public sealed record DevolucaoComandoResultado(long Id,string Situacao,long Version,bool Repetido);

public interface IDevolucaoCompraRepository
{
 Task<PagedResult<DevolucaoResumo>> ListarAsync(Guid tenant,DevolucaoFiltro filtro,CancellationToken ct);
 Task<DevolucaoDetalhe?> ObterAsync(Guid tenant,long id,CancellationToken ct);
 Task<OrigemDevolucao?> ObterOrigemAsync(Guid tenant,Guid recebimentoId,CancellationToken ct);
 Task<DevolucaoParaEdicao?> ObterParaEdicaoAsync(Guid tenant,long devolucaoId,CancellationToken ct);
 Task<IReadOnlyList<OrigemElegivelResumo>> PesquisarOrigensElegiveisAsync(Guid tenant,string? busca,CancellationToken ct);
 Task<IReadOnlyList<ResponsavelDivergencia>> PesquisarResponsaveisAsync(Guid tenant,string? busca,int pagina,int tamanho,CancellationToken ct);
 Task<ContextoInstitucionalSnapshot?> ObterContextoInstitucionalAsync(Guid tenant,Guid usuario,CancellationToken ct);
 Task<IReadOnlyList<DestinacaoRejeitadoItem>> ObterAcompanhamentoDestinacaoAsync(Guid tenant,Guid recebimentoId,CancellationToken ct);
 Task<IReadOnlyList<DevolucaoResumo>> ListarPorRecebimentoAsync(Guid tenant,Guid recebimentoId,CancellationToken ct);
 Task<DevolucaoComandoResultado> CriarAsync(ComprasContext context,CriarDevolucaoRequest request,CancellationToken ct);
 Task<DevolucaoComandoResultado> EditarAsync(ComprasContext context,long id,EditarDevolucaoRequest request,CancellationToken ct);
 Task<DevolucaoComandoResultado> ExpedirAsync(ComprasContext context,long id,ExpedirDevolucaoRequest request,CancellationToken ct);
 Task<DevolucaoComandoResultado> EntregarAsync(ComprasContext context,long id,EntregarDevolucaoRequest request,CancellationToken ct);
 Task<DevolucaoComandoResultado> CancelarAsync(ComprasContext context,long id,CancelarDevolucaoRequest request,CancellationToken ct);
}
public interface IDevolucaoCompraApplicationService : IDevolucaoCompraRepository { }

public sealed record RequisicaoEnvioResultado(Guid Id,string Status,long Version,int Ciclo,bool Repetido);

public sealed record AprovacaoFilaResumo(Guid EtapaId,int Nivel,decimal Limite,Guid? AprovadorId,bool Bloqueada,bool DecisivelPorMim,Guid RequisicaoId,string Numero,decimal Total,string StatusRequisicao,string Urgencia,DateTime SolicitadaEm,DateTime CriadaEm,long Version,string? SolicitanteNome=null,string? Setor=null,string? ProximaAcao=null);

public sealed record AprovacaoPainelResumo(Guid RequisicaoId,string Numero,decimal Total,string StatusRequisicao,string? Urgencia=null,DateTime SolicitadaEm=default,DateTime? DecididaEm=null,string? MotivoFinal=null,Guid? Aprovador=null);
public sealed record AprovacaoEtapaLinha(Guid EtapaId,int Nivel,decimal Limite,string Status,Guid? AprovadorId,string? AprovadorNome,DateTime? DecididaEm,string? Motivo);
public sealed record AprovacaoEtapaDetalhe(Guid EtapaId,int Ciclo,int Nivel,decimal Limite,string StatusEtapa,Guid? AprovadorId,string? AprovadorNome,bool Bloqueada,bool DecisivelPorMim,long VersionEtapa,DateTime? DecididaEm,string? Motivo,Guid RequisicaoId,string Numero,string StatusRequisicao,string Urgencia,string? Setor,string SolicitanteNome,DateTime SolicitadaEm,decimal Total,string? RegraSnapshot,IReadOnlyList<RequisicaoItemDetalhe> Itens,IReadOnlyList<AprovacaoEtapaLinha> EtapasCiclo,AprovacaoEtapaLinha? ProximaEtapa,IReadOnlyList<RequisicaoHistorico> Historico,string ClassificacaoSnapshot="COMPLETO",string? SnapshotDiagnostico=null);

/// <summary>
/// Interpretação canônica de quorum por nível: um nível anterior (k &lt; alvo) está "coberto"
/// quando ao menos uma etapa desse nível no ciclo está APROVADO — irmãos cancelados do mesmo
/// nível não impedem a progressão. Níveis distintos anteriores exigem cobertura cada um.
/// Centralizada para que fila, detalhe e decisão interpretem a mesma regra.
/// </summary>
public static class AprovacaoQuorum
{
 public static bool NivelAnteriorCoberto(IEnumerable<(int Nivel, string Status)> etapas, int nivelAlvo)
  => etapas.Where(e => e.Nivel < nivelAlvo)
   .GroupBy(e => e.Nivel)
   .All(g => g.Any(e => e.Status == "APROVADO"));
}

public sealed record AprovacaoDecisaoRequest(string? Decisao,string? Motivo,long Version,string? IdempotencyKey);

public sealed record AprovacaoDecisaoResultado(Guid EtapaId,string EtapaStatus,string RequisicaoStatus,bool Repetido);

public sealed record NivelPoliticaResumo(int Ordem,decimal Limite);

public sealed record PoliticaAtivaResumo(long Id,string Nome,string EsferaGoverno,string TipoEntidade,string? UnidadeGestora,string? UnidadeExecutora,DateTime AtualizadaEm,IReadOnlyList<NivelPoliticaResumo> Niveis);

public sealed record NivelPoliticaRequest(decimal Limite);

public sealed record SalvarPoliticaRequest(string? Nome,IReadOnlyList<NivelPoliticaRequest>? Niveis);

public sealed record PoliticaSalvaResultado(long Id,string Nome,bool Repetido);

public sealed record ReavaliarEncaminhamentoRequest(string? Motivo,string? IdempotencyKey);
public sealed record ReavaliarEncaminhamentoResultado(Guid RequisicaoId,int Ciclo,bool Desbloqueado,string Mensagem,bool Repetido);

public sealed record AprovacaoRelatorioLinha(string Numero,int Ciclo,int Etapa,string SituacaoEtapa,decimal Alcada,string? AprovadorSub,string StatusRequisicao,decimal Total,DateTime CriadaEm,DateTime? DecididaEm,string? Motivo);

public interface IAprovacaoRequisicaoRepository
{
 Task<PagedResult<AprovacaoFilaResumo>> ListarFilaAsync(ComprasContext context,int pagina,int tamanho,string? busca,string? urgencia,CancellationToken ct);
 Task<PagedResult<AprovacaoPainelResumo>> ListarDevolvidasAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
 Task<PagedResult<AprovacaoPainelResumo>> ListarConcluidasAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
 Task<AprovacaoEtapaDetalhe?> ObterDetalheAsync(ComprasContext context,Guid etapaId,CancellationToken ct);
 Task<AprovacaoDecisaoResultado> DecidirAsync(ComprasContext context,Guid etapaId,AprovacaoDecisaoRequest request,CancellationToken ct);
 Task<ReavaliarEncaminhamentoResultado> ReavaliarEncaminhamentoAsync(ComprasContext context,Guid requisicaoId,ReavaliarEncaminhamentoRequest request,CancellationToken ct);
 Task<PoliticaAtivaResumo?> ObterPoliticaAsync(ComprasContext context,CancellationToken ct);
 Task<PoliticaSalvaResultado> SalvarPoliticaAsync(ComprasContext context,SalvarPoliticaRequest request,string key,CancellationToken ct);
 Task<PagedResult<AprovacaoRelatorioLinha>> ListarRelatorioAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
}
public interface IAprovacaoRequisicaoApplicationService
{
 Task<PagedResult<AprovacaoFilaResumo>> ListarFilaAsync(ComprasContext context,int pagina,int tamanho,string? busca,string? urgencia,CancellationToken ct);
 Task<PagedResult<AprovacaoPainelResumo>> ListarDevolvidasAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
 Task<PagedResult<AprovacaoPainelResumo>> ListarConcluidasAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
 Task<AprovacaoEtapaDetalhe?> ObterDetalheAsync(ComprasContext context,Guid etapaId,CancellationToken ct);
 Task<AprovacaoDecisaoResultado> DecidirAsync(ComprasContext context,Guid etapaId,AprovacaoDecisaoRequest request,CancellationToken ct);
 Task<ReavaliarEncaminhamentoResultado> ReavaliarEncaminhamentoAsync(ComprasContext context,Guid requisicaoId,ReavaliarEncaminhamentoRequest request,CancellationToken ct);
 Task<PoliticaAtivaResumo?> ObterPoliticaAsync(ComprasContext context,CancellationToken ct);
 Task<PoliticaSalvaResultado> SalvarPoliticaAsync(ComprasContext context,SalvarPoliticaRequest request,string key,CancellationToken ct);
 Task<PagedResult<AprovacaoRelatorioLinha>> ListarRelatorioAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct);
}

// ===== Cotações, comparativo, seleção e pedidos (jornada procure-to-pay - Bloco B) =====
// Cotação só de requisição aprovada com saldo transacional por item; fornecedor convidado e
// resposta registrada internamente pelo operador autorizado; comparação por fórmula explícita;
// seleção humana e auditada gera pedido atômico reconhecido pelo recebimento existente.

/// <summary>Fórmula impressa em toda tela de comparativo para tornar a comparação explícita.</summary>
public static class FormulaComparacaoCotacao
{
 public const string Texto = "Política monetária unificada: Base de cálculo = Preço Unitário × Quantidade (Valor Bruto). Desconto = Valor Bruto × (Desconto% ÷ 100). Imposto = Valor Bruto × (Imposto% ÷ 100). Valor Líquido = Valor Bruto − Desconto + Imposto. Custo Unitário Efetivo (CUE) = Valor Líquido ÷ Quantidade. Frete = Frete do item rateado/informado. Custo Total do Item = Valor Líquido + Frete. Total do Pedido = Soma dos Totais dos Itens. Arredondamento monetário a 2 casas decimais (MidpointRounding.AwayFromZero) aplicado em cada etapa.";
}

public sealed record CotacaoFiltro(string? Status = null, Guid? RequisicaoId = null, int Pagina = 1, int Tamanho = 20);
public sealed record CotacaoResumo(Guid Id, string Numero, int Rodada, Guid RequisicaoId, string NumeroRequisicao, string Status, DateTime Prazo, DateTime CriadoEm, long Version, int Itens, int ConvitesAtivos, int Respostas);
public sealed record CotacaoElegivelResumo(Guid RequisicaoId, string Numero, string? Setor, string? Urgencia, decimal ValorEstimado, DateOnly? DataNecessaria, int ItensElegiveis, decimal SaldoDisponivelTotal);
public sealed record CotacaoElaboracaoItem(Guid RequisicaoItemId, int Ordem, string Tipo, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, decimal ReservaAtiva, decimal SaldoDisponivel, bool ExigeInspecao);
public sealed record CotacaoElaboracaoViewModel(Guid RequisicaoId, string Numero, string Status, string? Urgencia, string? Setor, decimal ValorEstimado, bool Elegivel, string? MotivoInelegibilidade, int ProximaRodada, IReadOnlyList<CotacaoElaboracaoItem> Itens, IReadOnlyList<CotacaoResumo> CoticacoesRelacionadas);
public sealed record CotacaoProdutoResumo(Guid Id, string Sku, string Nome, string Unidade);
public sealed record ProdutoLinhaRequest(Guid RequisicaoItemId, Guid ProdutoId);
public sealed record CriarCotacaoRequest(Guid RequisicaoId, IReadOnlyList<Guid> FornecedorIds, DateTime Prazo, IReadOnlyList<ProdutoLinhaRequest> Produtos, string? IdempotencyKey);
public sealed record CotacaoCriaResultado(Guid Id, string Numero, int Rodada, int Itens, int Convites, DateTime Prazo, bool Repetido);
public sealed record CotacaoItemDetalhe(long Id, Guid RequisicaoItemId, int Ordem, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, Guid ProdutoId, string ProdutoNome, string ProdutoSku);
public sealed record CotacaoConviteDetalhe(Guid Id, Guid FornecedorId, string FornecedorNome, string FornecedorDocumento, string Status, DateTime ExpiraEm, long Version, DateTime? Responder, int ItensRespondidos);
public sealed record CotacaoSelecaoLinha(long Id, Guid RequisicaoItemId, string Descricao, string Unidade, decimal Quantidade, Guid FornecedorId, string FornecedorNome, decimal CustoTotalItem, string? Justificativa);
public sealed record CotacaoPedidoGerado(Guid PedidoId, string Numero, Guid FornecedorId, string FornecedorNome, decimal ValorTotal, int Itens);
public sealed record CotacaoDetalhe(Guid Id, string Numero, int Rodada, Guid RequisicaoId, string NumeroRequisicao, string Status, DateTime Prazo, DateTime CriadoEm, DateTime? SelecionadoEm, long Version, IReadOnlyList<CotacaoItemDetalhe> Itens, IReadOnlyList<CotacaoConviteDetalhe> Convites, IReadOnlyList<CotacaoSelecaoLinha>? Selecoes, IReadOnlyList<CotacaoPedidoGerado>? Pedidos);
public sealed record CotacaoComparativoOferta(Guid ConviteId, Guid FornecedorId, string FornecedorNome, decimal PrecoUnitario, decimal Desconto, decimal Imposto, decimal Frete, decimal CustoUnitarioEfetivo, decimal CustoTotalItem, int PrazoDias, string? Marca, string? Fabricante, bool Recusado, bool MenorCusto, bool EmpateMenor);
public sealed record CotacaoComparativoLinha(Guid RequisicaoItemId, int Ordem, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, IReadOnlyList<CotacaoComparativoOferta> Ofertas, bool SemOfertas);
public sealed record CotacaoComparativoViewModel(Guid CotacaoId, string Numero, string Status, DateTime Prazo, long Version, string Formula, bool ElegivelParaSelecao, IReadOnlyList<CotacaoComparativoLinha> Linhas);
public sealed record RespostaItemRequest(Guid RequisicaoItemId, decimal PrecoUnitario, decimal Desconto, decimal Imposto, decimal Frete, int PrazoDias, string? Marca, string? Fabricante, bool Recusado);
public sealed record RegistrarRespostaRequest(Guid ConviteId, long ConviteVersion, IReadOnlyList<RespostaItemRequest> Itens, string? IdempotencyKey);
public sealed record RespostaRegistradaResultado(Guid CotacaoId, Guid ConviteId, string ConviteStatus, string CotacaoStatus, bool Repetido);
public sealed record SelecaoItemRequest(Guid RequisicaoItemId, Guid ConviteId, string? Justificativa);
public sealed record SelecionarItensRequest(long Version, IReadOnlyList<SelecaoItemRequest> Itens, string? IdempotencyKey);
public sealed record SelecaoConcluidaResultado(Guid CotacaoId, string Status, DateTime SelecionadoEm, IReadOnlyList<CotacaoPedidoGerado> Pedidos, bool Repetido);
public sealed record EncerrarCotacaoRequest(string Motivo, long Version, string? IdempotencyKey);
public sealed record CotacaoEncerradaResultado(Guid Id, string Status, bool Repetido);
public sealed record PedidoFiltro(string? Status = null, string? Busca = null, int Pagina = 1, int Tamanho = 20);
public sealed record PedidoResumo(Guid Id, string Numero, Guid? FornecedorId, string? FornecedorNome, string Status, decimal ValorTotal, DateOnly? Previsao, DateTime CriadoEm, Guid? RequisicaoId, Guid? CotacaoId, int Itens, long Version);

public sealed record PedidoItemDetalhe(
    long Id,
    Guid ProdutoId,
    string? ProdutoNome,
    string? Unidade,
    decimal Quantidade,
    decimal QuantidadeCancelada,
    decimal ValorUnitario,
    bool ExigeInspecao,
    decimal QuantidadeRecebidaFisica = 0m,
    decimal QuantidadeAguardandoInspecao = 0m,
    decimal QuantidadeAceita = 0m,
    decimal QuantidadeRejeitada = 0m,
    decimal QuantidadeDevolvida = 0m,
    decimal QuantidadePendenteRegularizacao = 0m,
    decimal SaldoEntregavel = 0m,
    decimal ValorBruto = 0m,
    decimal Desconto = 0m,
    decimal Imposto = 0m,
    decimal Frete = 0m,
    decimal ValorLiquido = 0m,
    decimal TotalItem = 0m,
    Guid? RequisicaoItemId = null,
    long? CotacaoItemId = null,
    long? CotacaoSelecaoId = null,
    decimal QuantidadeReposicaoAutorizada = 0m,
    decimal QuantidadeReposicaoRecebida = 0m,
    decimal QuantidadeReposicaoPendente = 0m);

public sealed record PedidoRecebimentoResumo(Guid Id, string Documento, string? AlmoxarifadoNome, DateTime DataOperacao, string Status, string ResultadoInspecao, int Divergencias);
public sealed record PedidoDivergenciaResumo(long Id, string Produto, decimal QuantidadeRejeitada, string Motivo, string Situacao, string? Providencia, string? Resultado, bool ReposicaoAutorizada = false, decimal QuantidadeReposicao = 0m, decimal ReposicaoRecebida = 0m, decimal SaldoReposicaoPendente = 0m, string? ReposicaoJustificativa = null);
public sealed record PedidoDevolucaoResumo(long Id, string Situacao, decimal QuantidadeTotal, string Motivo, DateTime CriadaEm, DateTime? ExpedidaEm, DateTime? EntregueEm, string? DocumentoProtocolo);
public sealed record PedidoProximaAcao(string Codigo, string Titulo, string Descricao, string RotaAcao, bool Disponivel);

public sealed record PedidoDetalhe(
    Guid Id,
    string Numero,
    string Status,
    Guid? FornecedorId,
    string? FornecedorNome,
    decimal ValorTotal,
    DateOnly? Previsao,
    DateTime CriadoEm,
    Guid? CotacaoId,
    string? NumeroCotacao,
    Guid? RequisicaoId,
    string? NumeroRequisicao,
    long Version,
    IReadOnlyList<PedidoItemDetalhe> Itens,
    IReadOnlyList<RequisicaoHistorico> Historico,
    decimal ValorBruto = 0m,
    decimal DescontoTotal = 0m,
    decimal ImpostoTotal = 0m,
    decimal FreteTotal = 0m,
    decimal ValorLiquido = 0m,
    string? MotivoEncerramento = null,
    DateTime? EncerradoEm = null,
    string? MotivoCancelamento = null,
    DateTime? CanceladoEm = null,
    IReadOnlyList<PedidoRecebimentoResumo>? Recebimentos = null,
    IReadOnlyList<PedidoDivergenciaResumo>? Divergencias = null,
    IReadOnlyList<PedidoDevolucaoResumo>? Devolucoes = null,
    PedidoProximaAcao? ProximaAcao = null);

public sealed record EncerrarPedidoRequest(long Version, string Motivo, bool CancelarSaldoRemanescente = true, string? IdempotencyKey = null);
public sealed record CancelarPedidoRequest(long Version, string Motivo, string? IdempotencyKey = null);
public sealed record PedidoComandoResultado(Guid Id, string Status, long Version, bool Repetido, string Mensagem);

public interface ICotacaoCompraRepository
{
 Task<PagedResult<CotacaoResumo>> ListarAsync(ComprasContext context, CotacaoFiltro filtro, CancellationToken ct);
 Task<PagedResult<CotacaoElegivelResumo>> ListarElegiveisAsync(ComprasContext context, string? busca, int pagina, int tamanho, CancellationToken ct);
 Task<CotacaoElaboracaoViewModel?> ObterElaboracaoAsync(ComprasContext context, Guid requisicaoId, CancellationToken ct);
 Task<IReadOnlyList<CotacaoProdutoResumo>> PesquisarProdutosAsync(ComprasContext context, string? busca, int limite, CancellationToken ct);
 Task<CotacaoCriaResultado> CriarAsync(ComprasContext context, CriarCotacaoRequest request, string key, CancellationToken ct);
 Task<CotacaoDetalhe?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<CotacaoComparativoViewModel?> ObterComparativoAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<RespostaRegistradaResultado> RegistrarRespostaAsync(ComprasContext context, Guid cotacaoId, RegistrarRespostaRequest request, string key, CancellationToken ct);
 Task<SelecaoConcluidaResultado> SelecionarAsync(ComprasContext context, Guid cotacaoId, SelecionarItensRequest request, string key, CancellationToken ct);
 Task<CotacaoEncerradaResultado> EncerrarAsync(ComprasContext context, Guid cotacaoId, EncerrarCotacaoRequest request, string key, CancellationToken ct);
 Task<PagedResult<PedidoResumo>> ListarPedidosAsync(ComprasContext context, PedidoFiltro filtro, CancellationToken ct);
 Task<PedidoDetalhe?> ObterPedidoAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<PedidoComandoResultado> EncerrarPedidoAsync(ComprasContext context, Guid pedidoId, EncerrarPedidoRequest request, string key, CancellationToken ct);
 Task<PedidoComandoResultado> CancelarPedidoAsync(ComprasContext context, Guid pedidoId, CancelarPedidoRequest request, string key, CancellationToken ct);
}
public interface ICotacaoCompraApplicationService
{
 Task<PagedResult<CotacaoResumo>> ListarAsync(ComprasContext context, CotacaoFiltro filtro, CancellationToken ct);
 Task<PagedResult<CotacaoElegivelResumo>> ListarElegiveisAsync(ComprasContext context, string? busca, int pagina, int tamanho, CancellationToken ct);
 Task<CotacaoElaboracaoViewModel?> ObterElaboracaoAsync(ComprasContext context, Guid requisicaoId, CancellationToken ct);
 Task<IReadOnlyList<CotacaoProdutoResumo>> PesquisarProdutosAsync(ComprasContext context, string? busca, int limite, CancellationToken ct);
 Task<CotacaoCriaResultado> CriarAsync(ComprasContext context, CriarCotacaoRequest request, string key, CancellationToken ct);
 Task<CotacaoDetalhe?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<CotacaoComparativoViewModel?> ObterComparativoAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<RespostaRegistradaResultado> RegistrarRespostaAsync(ComprasContext context, Guid cotacaoId, RegistrarRespostaRequest request, string key, CancellationToken ct);
 Task<SelecaoConcluidaResultado> SelecionarAsync(ComprasContext context, Guid cotacaoId, SelecionarItensRequest request, string key, CancellationToken ct);
 Task<CotacaoEncerradaResultado> EncerrarAsync(ComprasContext context, Guid cotacaoId, EncerrarCotacaoRequest request, string key, CancellationToken ct);
 Task<PagedResult<PedidoResumo>> ListarPedidosAsync(ComprasContext context, PedidoFiltro filtro, CancellationToken ct);
 Task<PedidoDetalhe?> ObterPedidoAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<PedidoComandoResultado> EncerrarPedidoAsync(ComprasContext context, Guid pedidoId, EncerrarPedidoRequest request, string key, CancellationToken ct);
 Task<PedidoComandoResultado> CancelarPedidoAsync(ComprasContext context, Guid pedidoId, CancelarPedidoRequest request, string key, CancellationToken ct);
}

public sealed record FaturaFiltro(string? Busca = null, string? Status = null, Guid? FornecedorId = null, Guid? PedidoId = null, int Pagina = 1, int Tamanho = 20);
public sealed record FaturaResumo(Guid Id, string Numero, string Serie, string TipoDocumento, Guid FornecedorId, string FornecedorNome, Guid PedidoId, string PedidoNumero, decimal Total, decimal ValorLiquido, string Status, string ResultadoMatch, DateTimeOffset CriadaEm, DateTime? DataEmissao, DateTime? DataVencimento, int ItensCount, long Version);
public sealed record FaturaItemDetalhe(
    long Id,
    long PedidoItemId,
    string ProdutoNome,
    string Unidade,
    decimal QuantidadePedida,
    decimal QuantidadeCancelada,
    decimal QuantidadeVigente,
    decimal QuantidadeAceitaTotal,
    decimal QuantidadeAprovadaOutras,
    decimal QuantidadeReservadaOutras,
    decimal SaldoDisponivel,
    decimal QuantidadeDeclarada,
    decimal QuantidadeReservada,
    decimal QuantidadeAprovada,
    decimal PrecoUnitarioPedido,
    decimal PrecoUnitarioFatura,
    decimal TotalItem,
    string StatusItem,
    string? Diagnostico)
{
    public decimal QuantidadeFaturadaOutras => QuantidadeAprovadaOutras + QuantidadeReservadaOutras;
    public decimal SaldoFaturavelElegivel => SaldoDisponivel;
    public decimal QuantidadeFaturada => QuantidadeDeclarada;
}

public sealed record FaturaEventoDetalhe(long Id, string Tipo, string? Detalhes, Guid UsuarioId, string? Autor, DateTimeOffset OcorridoEm, string? CorrelationId);
public sealed record FaturaDetalhe(Guid Id, Guid TenantId, string Numero, string Serie, string TipoDocumento, string? ChaveAcesso, Guid FornecedorId, string FornecedorNome, string FornecedorCnpj, Guid PedidoId, string PedidoNumero, decimal Total, decimal ValorItens, decimal ValorDesconto, decimal ValorFrete, decimal ValorSeguro, decimal ValorOutrasDespesas, decimal ValorLiquido, string Status, string ResultadoMatch, DateTime? DataEmissao, DateTime? DataVencimento, string? Observacoes, string? Justificativa, string? DecididoPor, DateTimeOffset? DecididoEm, string? MotivoRejeicao, DateTimeOffset CriadaEm, long Version, IReadOnlyList<FaturaItemDetalhe> Itens, IReadOnlyList<FaturaEventoDetalhe> Historico);
public sealed record CriarFaturaItemRequest(long PedidoItemId, decimal Quantidade, decimal ValorUnitario);
public sealed record CriarFaturaRequest(Guid PedidoId, Guid FornecedorId, string Numero, string Serie, string TipoDocumento, string? ChaveAcesso, DateTime? DataEmissao, DateTime? DataVencimento, decimal ValorDesconto, decimal ValorFrete, decimal ValorSeguro, decimal ValorOutrasDespesas, string? Observacoes, IReadOnlyList<CriarFaturaItemRequest> Itens);
public sealed record DecidirFaturaRequest(long Version, string Decisao, string? Justificativa, string? IdempotencyKey = null);
public sealed record FaturaComandoResultado(Guid Id, string Status, string ResultadoMatch, long Version, bool Repetido);

public sealed record FaturaConferenciaPreviaItem(
    long PedidoItemId,
    string ProdutoNome,
    string Unidade,
    decimal QuantidadePedida,
    decimal QuantidadeCancelada,
    decimal QuantidadeVigente,
    decimal QuantidadeAceitaTotal,
    decimal QuantidadeAprovadaOutras,
    decimal QuantidadeReservadaOutras,
    decimal SaldoDisponivel,
    decimal ValorUnitarioPedido,
    string? MotivoSaldoZero = null)
{
    public decimal QuantidadeFaturadaOutras => QuantidadeAprovadaOutras + QuantidadeReservadaOutras;
    public decimal SaldoFaturavelElegivel => SaldoDisponivel;
}

public sealed record FaturaConferenciaPrevia(Guid PedidoId, string PedidoNumero, Guid FornecedorId, string FornecedorNome, string FornecedorCnpj, decimal TotalPedido, IReadOnlyList<FaturaConferenciaPreviaItem> Itens);

public interface IFaturaCompraRepository
{
    Task<PagedResult<FaturaResumo>> ListarAsync(ComprasContext context, FaturaFiltro filtro, CancellationToken ct);
    Task<FaturaDetalhe?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
    Task<FaturaConferenciaPrevia> ObterConferenciaPreviaAsync(ComprasContext context, Guid pedidoId, CancellationToken ct);
    Task<FaturaComandoResultado> CriarAsync(ComprasContext context, CriarFaturaRequest request, string key, CancellationToken ct);
    Task<FaturaComandoResultado> DecidirAsync(ComprasContext context, Guid id, DecidirFaturaRequest request, CancellationToken ct);
}

public interface IFaturaCompraApplicationService
{
    Task<PagedResult<FaturaResumo>> ListarAsync(ComprasContext context, FaturaFiltro filtro, CancellationToken ct);
    Task<FaturaDetalhe?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
    Task<FaturaConferenciaPrevia> ObterConferenciaPreviaAsync(ComprasContext context, Guid pedidoId, CancellationToken ct);
    Task<FaturaComandoResultado> CriarAsync(ComprasContext context, CriarFaturaRequest request, string key, CancellationToken ct);
    Task<FaturaComandoResultado> DecidirAsync(ComprasContext context, Guid id, DecidirFaturaRequest request, CancellationToken ct);
}

