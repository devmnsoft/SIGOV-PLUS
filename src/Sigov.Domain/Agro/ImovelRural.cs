using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ImovelRural : AggregateRoot
{
    public ImovelRural(long tenantId, long entidadeId, long propriedadeId)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (propriedadeId <= 0) throw new ArgumentException("Imóvel rural exige propriedade.", nameof(propriedadeId));
        TenantId = tenantId; EntidadeId = entidadeId; PropriedadeId = propriedadeId;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long PropriedadeId { get; }
}
