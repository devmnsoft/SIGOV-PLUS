using Sigov.Domain.Saas;

namespace Sigov.Application.Saas.Profiles;

public sealed class ProfileLevelService : IProfileLevelService
{
    private readonly IProfileLevelRepository _repository;

    public ProfileLevelService(IProfileLevelRepository repository) => _repository = repository;

    public Task<IReadOnlyCollection<ProfileLevelItem>> GetLevelsAsync(CancellationToken cancellationToken) => _repository.GetLevelsAsync(cancellationToken);

    public bool IsGlobalAdmin(IEnumerable<string> profileCodes) => profileCodes.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);

    public bool IsTenantAdmin(IEnumerable<string> profileCodes) => profileCodes.Any(code => string.Equals(code, PerfilNivelCodigos.AdministradorTenant, StringComparison.OrdinalIgnoreCase));
}
