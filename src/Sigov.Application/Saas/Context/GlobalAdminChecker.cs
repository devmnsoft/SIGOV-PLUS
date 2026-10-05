using Sigov.Domain.Saas;

namespace Sigov.Application.Saas.Context;

public sealed class GlobalAdminChecker : IGlobalAdminChecker
{
    private readonly ITenantContextSwitchRepository _repository;

    public GlobalAdminChecker(ITenantContextSwitchRepository repository) => _repository = repository;

    public bool IsGlobalAdmin(IEnumerable<string> profileCodes) => profileCodes.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);

    public async Task<bool> IsGlobalAdminAsync(long usuarioId, CancellationToken cancellationToken)
    {
        var profiles = await _repository.GetUserProfileCodesAsync(usuarioId, cancellationToken).ConfigureAwait(false);
        return IsGlobalAdmin(profiles);
    }
}
