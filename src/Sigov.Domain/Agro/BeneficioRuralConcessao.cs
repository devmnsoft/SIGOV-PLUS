using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class BeneficioRuralConcessao : AggregateRoot
{
    public BeneficioRuralConcessao(long tenantId, long entidadeId, long? exercicioId, long beneficioId, long produtorId, string numero, decimal? quantidade, decimal? valor, AgroBeneficioStatus status, long? autorizadoBy = null, DateTimeOffset? autorizadoAt = null, long? entregueBy = null, DateTimeOffset? entregueAt = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (beneficioId <= 0) throw new ArgumentException("Concessão exige benefício.", nameof(beneficioId));
        if (produtorId <= 0) throw new ArgumentException("Concessão exige produtor.", nameof(produtorId));
        if (quantidade < 0) throw new ArgumentException("Quantidade não pode ser negativa.", nameof(quantidade));
        if (valor < 0) throw new ArgumentException("Valor não pode ser negativo.", nameof(valor));
        if (status == AgroBeneficioStatus.AUTORIZADO && (!autorizadoBy.HasValue || autorizadoBy <= 0)) throw new ArgumentException("Concessão autorizada exige usuário autorizador.", nameof(autorizadoBy));
        if (status == AgroBeneficioStatus.ENTREGUE && (!entregueBy.HasValue || entregueBy <= 0 || !entregueAt.HasValue)) throw new ArgumentException("Concessão entregue exige usuário e data.", nameof(entregueBy));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; BeneficioId = beneficioId; ProdutorId = produtorId; Numero = Required(numero, "Número da concessão é obrigatório."); Quantidade = quantidade; Valor = valor; Status = status;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public long BeneficioId { get; }
    public long ProdutorId { get; }
    public string Numero { get; }
    public decimal? Quantidade { get; }
    public decimal? Valor { get; }
    public AgroBeneficioStatus Status { get; }
    public void Entregar(long usuarioId, DateTimeOffset data) { if (Status is AgroBeneficioStatus.CANCELADO or AgroBeneficioStatus.INDEFERIDO) throw new InvalidOperationException("Concessão cancelada ou indeferida não pode ser entregue."); if (usuarioId <= 0) throw new ArgumentException("Entrega exige usuário.", nameof(usuarioId)); }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
