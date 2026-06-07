namespace Sigov.Application.Saas.Tenants;

public sealed class TenantAuditWriter
{
    public string BuildResourceKey(long tenantId) => $"sigov.tenant:{tenantId}";
}
