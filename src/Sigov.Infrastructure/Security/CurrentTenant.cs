using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Saas;

namespace Sigov.Infrastructure.Security;

public sealed class CurrentTenant : ICurrentTenant
{
    private readonly ITenantContext _tenantContext;
    private readonly IRequestAuthorizationSnapshot _authorization;

    public CurrentTenant(ITenantContext tenantContext, IRequestAuthorizationSnapshot authorization)
    {
        _tenantContext = tenantContext;
        _authorization = authorization;
    }

    // The API middleware materializes ITenantContext. MVC validates the cookie and
    // its persisted access in RequestAuthorizationSnapshotMiddleware instead. Both
    // hosts therefore consume the same request-scoped, server-validated snapshot;
    // a value supplied by a request parameter is never considered here.
    public long? TenantId => _tenantContext.TenantId ?? _authorization.Current.TenantId;
    public string? TenantSlug => _tenantContext.TenantSlug;
    public long? EntidadeId => _authorization.Current.EntidadeId;
    public long? ExercicioId => _authorization.Current.ExercicioId;
}
