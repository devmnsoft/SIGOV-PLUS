namespace Sigov.Application.Saas.Modules;

public interface IModuleCatalogService
{
    Task<IReadOnlyCollection<ModuleCatalogItem>> GetModulesAsync(CancellationToken cancellationToken = default);
    Task<ModuleCatalogItem?> FindByCodeAsync(string codigo, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ModulePackageItem>> GetPackagesAsync(CancellationToken cancellationToken = default);
    Task<ModulePackageItem?> FindPackageByCodeAsync(string codigo, CancellationToken cancellationToken = default);
}
