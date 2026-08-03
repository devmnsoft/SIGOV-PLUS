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
public sealed record RequisicaoResumo(Guid Id, string Numero, string Status, decimal ValorEstimado, DateOnly? DataNecessaria, int Itens, long Version);
public sealed record ComprasDashboard(decimal TotalSolicitado, decimal ValorAprovado, int AprovacoesPendentes, int CotacoesAbertas, int PedidosAtrasados, int RecebimentosPendentes, int FaturasBloqueadas, int DocumentosVencendo);

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
 Task<PagedResult<RequisicaoResumo>> ListarAsync(Guid tenant, int pagina, int tamanho, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarRequisicaoRequest request, string key, CancellationToken ct);
 Task EnviarAsync(ComprasContext context, Guid id, long version, CancellationToken ct);
}
public interface IComprasDashboardRepository { Task<ComprasDashboard> ObterAsync(Guid tenant, CancellationToken ct); }
public interface IFornecedorApplicationService
{
 Task<PagedResult<FornecedorResumo>> ListarAsync(ComprasContext context, FornecedorFiltro filtro, CancellationToken ct); Task<FornecedorResumo?> ObterAsync(ComprasContext context, Guid id, CancellationToken ct);
 Task<Guid> CriarAsync(ComprasContext context, CriarFornecedorRequest request, string key, CancellationToken ct); Task AlterarStatusAsync(ComprasContext context, Guid id, AlterarStatusRequest request, CancellationToken ct);
 Task AdicionarContatoAsync(ComprasContext context, Guid id, AdicionarContatoRequest request, string key, CancellationToken ct); Task AdicionarEnderecoAsync(ComprasContext context, Guid id, AdicionarEnderecoRequest request, string key, CancellationToken ct); Task AdicionarDocumentoAsync(ComprasContext context, Guid id, AdicionarDocumentoRequest request, string key, CancellationToken ct);
}
public interface IRequisicaoCompraApplicationService { Task<PagedResult<RequisicaoResumo>> ListarAsync(ComprasContext context,int pagina,int tamanho,CancellationToken ct); Task<Guid> CriarAsync(ComprasContext context,CriarRequisicaoRequest request,string key,CancellationToken ct); Task EnviarAsync(ComprasContext context,Guid id,long version,CancellationToken ct); }
public interface IComprasDashboardApplicationService { Task<ComprasDashboard> ObterAsync(ComprasContext context,CancellationToken ct); }
