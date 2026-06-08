using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class DocumentoRural : AggregateRoot
{
    public DocumentoRural(long tenantId, long entidadeId, string tipoDocumento, long? produtorId = null, long? propriedadeId = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (string.IsNullOrWhiteSpace(tipoDocumento)) throw new ArgumentException("Tipo de documento é obrigatório.", nameof(tipoDocumento));
        TenantId = tenantId; EntidadeId = entidadeId; TipoDocumento = tipoDocumento.Trim(); ProdutorId = produtorId; PropriedadeId = propriedadeId;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ProdutorId { get; }
    public long? PropriedadeId { get; }
    public string TipoDocumento { get; }
}
