using Sigov.Application.Abstractions;
using Sigov.Application.Saas;

namespace Sigov.Infrastructure.Security;

public sealed class CurrentTenant : ICurrentTenant
{
    private readonly ITenantContext _tenantContext;

    public CurrentTenant(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public long? TenantId => _tenantContext.TenantId;
    public string? TenantSlug => _tenantContext.TenantSlug;
    public long? EntidadeId => null;
    public long? ExercicioId => null;
}
