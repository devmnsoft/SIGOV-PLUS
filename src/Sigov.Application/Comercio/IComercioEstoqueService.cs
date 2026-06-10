namespace Sigov.Application.Comercio;

public interface IComercioEstoqueService
{
    Task ReservarEstoqueAsync(long tenantId, long origemId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken);
    Task BaixarEstoqueVendaAsync(long tenantId, long vendaId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken);
    Task EstornarEstoqueVendaAsync(long tenantId, long vendaId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken);
    Task<bool> VerificarDisponibilidadeAsync(long tenantId, long produtoId, decimal quantidade, bool permiteNegativo, CancellationToken cancellationToken);
    Task<decimal> ObterSaldoProdutoAsync(long tenantId, long produtoId, CancellationToken cancellationToken);
}

public sealed record ComercioEstoqueItem(long ProdutoId, decimal Quantidade, string Origem, long OrigemId);
