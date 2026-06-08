using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class DistribuicaoInsumo : AggregateRoot
{
    public DistribuicaoInsumo(long tenantId, long entidadeId, long? exercicioId, long insumoId, long produtorId, string numero, decimal quantidade, decimal? valorEstimado)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (insumoId <= 0) throw new ArgumentException("Distribuição exige insumo.", nameof(insumoId));
        if (produtorId <= 0) throw new ArgumentException("Distribuição exige produtor.", nameof(produtorId));
        if (quantidade <= 0) throw new ArgumentException("Quantidade distribuída deve ser maior que zero.", nameof(quantidade));
        if (valorEstimado < 0) throw new ArgumentException("Valor estimado não pode ser negativo.", nameof(valorEstimado));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; InsumoId = insumoId; ProdutorId = produtorId; Numero = Required(numero, "Número é obrigatório."); Quantidade = quantidade; ValorEstimado = valorEstimado;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public long InsumoId { get; }
    public long ProdutorId { get; }
    public string Numero { get; }
    public decimal Quantidade { get; }
    public decimal? ValorEstimado { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
