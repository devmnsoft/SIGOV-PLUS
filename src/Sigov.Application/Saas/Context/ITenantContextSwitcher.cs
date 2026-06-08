namespace Sigov.Application.Saas.Context;

public interface ITenantContextSwitcher
{
    Task<TenantContextSwitchResult> SwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken);
    Task<TenantContextSwitchResult> FinishAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken);
}
