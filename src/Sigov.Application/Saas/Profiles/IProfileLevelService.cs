namespace Sigov.Application.Saas.Profiles;

public interface IProfileLevelService
{
    Task<IReadOnlyCollection<ProfileLevelItem>> GetLevelsAsync(CancellationToken cancellationToken);
    bool IsGlobalAdmin(IEnumerable<string> profileCodes);
    bool IsTenantAdmin(IEnumerable<string> profileCodes);
}
