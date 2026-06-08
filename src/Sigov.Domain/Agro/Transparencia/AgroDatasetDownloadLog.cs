using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Transparencia;

public sealed class AgroDatasetDownloadLog : Entity
{
    public AgroDatasetDownloadLog(long tenantId, long? datasetId, long? publicacaoId, string? formato, string? ip)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (!datasetId.HasValue && !publicacaoId.HasValue) throw new ArgumentException("Download público deve registrar dataset ou publicação.");
        TenantId = tenantId; DatasetId = datasetId; PublicacaoId = publicacaoId; Formato = formato; Ip = ip;
    }
    public long TenantId { get; } public long? DatasetId { get; } public long? PublicacaoId { get; } public string? Formato { get; } public string? Ip { get; }
}
