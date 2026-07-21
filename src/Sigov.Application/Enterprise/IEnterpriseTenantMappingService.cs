namespace Sigov.Application.Enterprise;

public interface IEnterpriseTenantMappingService
{
    Task<Guid?> ResolveEnterpriseTenantAsync(long coreTenantId, CancellationToken cancellationToken);

    Task<long?> ResolveCoreTenantAsync(Guid enterpriseTenantId, CancellationToken cancellationToken);
}
