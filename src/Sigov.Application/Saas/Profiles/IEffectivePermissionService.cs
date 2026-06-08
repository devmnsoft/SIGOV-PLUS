namespace Sigov.Application.Saas.Profiles;

public interface IEffectivePermissionService
{
    Task<EffectivePermissionResult> CalculateAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken);
}
