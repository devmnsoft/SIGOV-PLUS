namespace Sigov.Application.Saas.Modules;

public interface IModuleCatalogService
{
    IReadOnlyCollection<ModuleCatalogItem> GetModules();
    ModuleCatalogItem? FindByCode(string codigo);
    IReadOnlyCollection<ModulePackageItem> GetPackages();
    ModulePackageItem? FindPackageByCode(string codigo);
}
