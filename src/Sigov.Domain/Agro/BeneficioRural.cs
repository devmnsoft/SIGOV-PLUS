using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class BeneficioRural : AggregateRoot
{
    public BeneficioRural(long tenantId, long entidadeId, string codigo, string nome, AgroBeneficioTipo tipoBeneficio, decimal? valorReferencia = null, decimal? quantidadeLimite = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Benefício exige nome.", nameof(nome));
        if (valorReferencia < 0) throw new ArgumentException("Valor de referência não pode ser negativo.", nameof(valorReferencia));
        if (quantidadeLimite < 0) throw new ArgumentException("Quantidade limite não pode ser negativa.", nameof(quantidadeLimite));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Benefício exige código."); Nome = nome.Trim(); TipoBeneficio = tipoBeneficio; ValorReferencia = valorReferencia; QuantidadeLimite = quantidadeLimite;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public AgroBeneficioTipo TipoBeneficio { get; }
    public decimal? ValorReferencia { get; }
    public decimal? QuantidadeLimite { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
