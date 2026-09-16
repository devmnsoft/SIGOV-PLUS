using Sigov.Application.Common;

namespace Sigov.Application.ComprasEmpresariais;

public sealed record ComprasContext(Guid TenantId, Guid UsuarioId, string CorrelationId);
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
public sealed record RequisicaoResumo(Guid Id, string Numero, string Status, decimal ValorEstimado, DateOnly? DataNecessaria, DateTimeOffset DataSolicitacao, string Origem, int Itens, long Version);
public sealed record RequisicaoItemDetalhe(Guid Id, int Ordem, string Tipo, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, decimal ValorEstimado, bool PermiteParcial, bool ExigeInspecao);
public sealed record RequisicaoHistorico(string Acao, string? Detalhes, DateTimeOffset CriadoEm);
public sealed record RequisicaoDetalhe(Guid Id, string Numero, string Status, string? Setor, string Urgencia, DateOnly? DataNecessaria, string Justificativa, string? Observacoes, decimal ValorEstimado, long Version, IReadOnlyList<RequisicaoItemDetalhe> Itens, IReadOnlyList<RequisicaoHistorico> Historico);
public sealed record ComprasDashboard(decimal TotalSolicitado, decimal ValorAprovado, int AprovacoesPendentes, int CotacoesAbertas, int PedidosAtrasados, int RecebimentosPendentes, int FaturasBloqueadas, int DocumentosVencendo);
public sealed record RecebimentoFiltro(string? Fornecedor = null, string? Pedido = null, DateOnly? DataInicial = null, DateOnly? DataFinal = null, string? Status = null, Guid? AlmoxarifadoId = null, string? Responsavel = null, int Pagina = 1, int Tamanho = 20);
public sealed record RecebimentoResumo(Guid Id, Guid PedidoId, string PedidoNumero, string FornecedorNome, string Status, string Documento, string AlmoxarifadoNome, DateTimeOffset CriadoEm, string Responsavel, int Divergencias);
public sealed record RecebimentoTotais(long Aptos, long EmConferencia, long Concluidos, long Divergencias);
public sealed record CentralRecebimentos(Common.PagedResult<RecebimentoResumo> Resultado, RecebimentoTotais Totais);
public sealed record PedidoRecebimentoItem(long Id, Guid ProdutoId, string Produto, string Unidade, decimal QuantidadePedida, decimal QuantidadeCancelada, decimal QuantidadeFisica, decimal QuantidadeAceita, decimal QuantidadeRejeitada, decimal QuantidadeEmConferencia, decimal QuantidadePendente, bool ExigeInspecao);
public sealed record AlmoxarifadoOpcao(Guid Id,string Nome);
public sealed record PedidoParaRecebimento(Guid Id, string Numero, string Status, Guid FornecedorId, string Fornecedor, long Version, IReadOnlyList<PedidoRecebimentoItem> Itens,IReadOnlyList<AlmoxarifadoOpcao> Almoxarifados);
public sealed record RecebimentoItemRequest(long PedidoItemId, decimal Quantidade, string? Lote, DateOnly? Validade, string? NumeroSerie);
public sealed record CriarRecebimentoRequest(Guid PedidoId, Guid AlmoxarifadoId, string Documento, DateTimeOffset DataOperacao, string? Observacoes, IReadOnlyList<RecebimentoItemRequest> Itens, long PedidoVersion);
public sealed record RecebimentoCriado(Guid Id, string Status, bool Repetido);

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
public interface IRequisicaoCompraRepository
{
 Task<PagedResult<RequisicaoResumo>> ListarAsync(Guid tenant, RequisicaoFiltro filtro, CancellationToken ct);
 Task<RequisicaoDetalhe?> ObterAsync(Guid tenant, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarRequisicaoRequest request, string key, CancellationToken ct);
 Task AtualizarAsync(ComprasContext context, Guid id, AtualizarRequisicaoRequest request, CancellationToken ct);
 Task EnviarAsync(ComprasContext context, Guid id, long version, CancellationToken ct);
}
public interface IComprasDashboardRepository { Task<ComprasDashboard> ObterAsync(Guid tenant, CancellationToken ct); }
public interface IRecebimentoCompraRepository
{
 Task<CentralRecebimentos> ListarAsync(Guid tenant, RecebimentoFiltro filtro, CancellationToken ct);
 Task<PedidoParaRecebimento?> ObterPedidoAsync(Guid tenant, Guid pedidoId, CancellationToken ct);
 Task<RecebimentoCriado> CriarEConfirmarAsync(ComprasContext context, CriarRecebimentoRequest request, string key, CancellationToken ct);
}
public interface IFornecedorApplicationService
{
 Task<PagedResult<FornecedorResumo>> ListarAsync(ComprasContext context, FornecedorFiltro filtro, CancellationToken ct); Task<FornecedorResumo?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarFornecedorRequest request, string key, CancellationToken ct); Task AlterarStatusAsync(ComprasContext context, Guid id, AlterarStatusRequest request, CancellationToken ct);
 Task AdicionarContatoAsync(ComprasContext context, Guid id, AdicionarContatoRequest request, string key, CancellationToken ct); Task AdicionarEnderecoAsync(ComprasContext context, Guid id, AdicionarEnderecoRequest request, string key, CancellationToken ct); Task AdicionarDocumentoAsync(ComprasContext context, Guid id, AdicionarDocumentoRequest request, string key, CancellationToken ct);
}
public interface IRequisicaoCompraApplicationService { Task<PagedResult<RequisicaoResumo>> ListarAsync(ComprasContext context,RequisicaoFiltro filtro,CancellationToken ct); Task<RequisicaoDetalhe?> ObterAsync(ComprasContext context,Guid id,CancellationToken ct); Task<Guid> CriarAsync(ComprasContext context,CriarRequisicaoRequest request,string key,CancellationToken ct); Task AtualizarAsync(ComprasContext context,Guid id,AtualizarRequisicaoRequest request,CancellationToken ct); Task EnviarAsync(ComprasContext context,Guid id,long version,CancellationToken ct); }
public interface IComprasDashboardApplicationService { Task<ComprasDashboard> ObterAsync(ComprasContext context,CancellationToken ct); }
public interface IRecebimentoCompraApplicationService
{
 Task<CentralRecebimentos> ListarAsync(ComprasContext context, RecebimentoFiltro filtro, CancellationToken ct);
 Task<PedidoParaRecebimento?> ObterPedidoAsync(ComprasContext context, Guid pedidoId, CancellationToken ct);
 Task<RecebimentoCriado> CriarEConfirmarAsync(ComprasContext context, CriarRecebimentoRequest request, string key, CancellationToken ct);
}
