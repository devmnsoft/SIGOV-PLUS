namespace Sigov.Application.Saas.Profiles;

public interface IProfileLevelRepository
{
    Task<IReadOnlyCollection<ProfileLevelItem>> GetLevelsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserAccessScope>> GetUserScopesAsync(long usuarioId, long? tenantId, CancellationToken cancellationToken);
}

public sealed record ProfileLevelItem(string Codigo, string Nome, string? Descricao, int NivelHierarquico, bool Global, bool TenantAdmin, bool Ativo);
