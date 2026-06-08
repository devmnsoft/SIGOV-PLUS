using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Transparencia;

public sealed class AgroDatasetPublicacao : Entity
{
    public AgroDatasetPublicacao(long tenantId, long datasetId, AgroDatasetStatus status, string formato)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (datasetId <= 0) throw new ArgumentException("Dataset é obrigatório.", nameof(datasetId));
        TenantId = tenantId; DatasetId = datasetId; Status = status; Formato = string.IsNullOrWhiteSpace(formato) ? throw new ArgumentException("Formato é obrigatório.", nameof(formato)) : formato.Trim().ToUpperInvariant();
    }
    public long TenantId { get; } public long DatasetId { get; } public AgroDatasetStatus Status { get; } public string Formato { get; }
    public void ValidarDownloadPublico() { if (Status != AgroDatasetStatus.PUBLICADO) throw new InvalidOperationException("Publicação pública exige status PUBLICADO."); }
}
