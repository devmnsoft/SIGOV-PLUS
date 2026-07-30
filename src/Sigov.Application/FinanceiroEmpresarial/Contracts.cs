using Sigov.Application.Common;

namespace Sigov.Application.FinanceiroEmpresarial;

public sealed record ContaFinanceiraFiltro(int Page = 1, int PageSize = 20, string? Status = null, DateOnly? VencimentoInicio = null, DateOnly? VencimentoFim = null);
public sealed record CriarContaReceberRequest(string Descricao, decimal Valor, DateOnly Vencimento, long? ClienteId, long? NaturezaId, long? CentroCustoId, string? DocumentoReferencia, string IdempotencyKey);
public sealed record CriarContaPagarRequest(string Descricao, decimal Valor, DateOnly Vencimento, long? FornecedorId, long? NaturezaId, long? CentroCustoId, string? DocumentoReferencia, string IdempotencyKey);
public sealed record ContaFinanceiraResumoDto(long Id, string Descricao, decimal ValorOriginal, decimal ValorAberto, DateOnly Vencimento, string Status, long Version, string Origem);
public sealed record BaixarContaRequest(decimal Valor, decimal Desconto, decimal Acrescimo, long? ContaBancariaId, long? CaixaId, long? FormaPagamentoId, long Version, string IdempotencyKey, string? Observacao);
public sealed record EstornarBaixaRequest(long BaixaId, string Motivo, long Version, string IdempotencyKey);
public sealed record TransferirValoresRequest(long ContaOrigemId, long ContaDestinoId, decimal Valor, string Descricao, string IdempotencyKey);
public sealed record BaixaFinanceiraDto(long TituloId, long BaixaId, long MovimentoId, decimal ValorEfetivo, decimal SaldoAberto, decimal SaldoDestino, string Status, long Version);
public sealed record TransferenciaFinanceiraDto(long TransferenciaId, long MovimentoSaidaId, long MovimentoEntradaId, decimal SaldoOrigem, decimal SaldoDestino);
public sealed record FinanceiroDashboardDto(decimal SaldoConsolidado, decimal ContasReceberAberto, decimal ContasPagarAberto, decimal RecebidoPeriodo, decimal PagoPeriodo, int TitulosVencidos, int IntegracoesPendentes);
public sealed record FluxoCaixaDto(DateOnly Data, decimal EntradasPrevistas, decimal SaidasPrevistas, decimal EntradasRealizadas, decimal SaidasRealizadas, decimal SaldoPrevisto, decimal SaldoRealizado);

public interface IFinanceiroEmpresarialRepository
{
    Task<PagedResult<ContaFinanceiraResumoDto>> ListarReceberAsync(long tenantId, ContaFinanceiraFiltro filtro, CancellationToken ct);
    Task<PagedResult<ContaFinanceiraResumoDto>> ListarPagarAsync(long tenantId, ContaFinanceiraFiltro filtro, CancellationToken ct);
    Task<long> CriarReceberAsync(long tenantId, CriarContaReceberRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<long> CriarPagarAsync(long tenantId, CriarContaPagarRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<BaixaFinanceiraDto> BaixarReceberAsync(long tenantId, long id, BaixarContaRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<BaixaFinanceiraDto> BaixarPagarAsync(long tenantId, long id, BaixarContaRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<BaixaFinanceiraDto> EstornarReceberAsync(long tenantId, long id, EstornarBaixaRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<BaixaFinanceiraDto> EstornarPagarAsync(long tenantId, long id, EstornarBaixaRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<TransferenciaFinanceiraDto> TransferirAsync(long tenantId, TransferirValoresRequest request, long? usuarioId, Guid correlationId, CancellationToken ct);
    Task<FinanceiroDashboardDto> DashboardAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<IReadOnlyList<FluxoCaixaDto>> FluxoCaixaAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct);
}
