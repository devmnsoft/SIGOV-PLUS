using Sigov.Application.Saas;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantContext : ITenantContext
{
    public long? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public string? Status { get; private set; }
    public bool IsResolved => TenantId.HasValue;

    public void SetTenant(long tenantId, string slug, string status)
    {
        TenantId = tenantId;
        TenantSlug = slug;
        Status = status;
    }

    public void Clear()
    {
        TenantId = null;
        TenantSlug = null;
        Status = null;
    }
}
