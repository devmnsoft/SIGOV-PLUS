namespace Sigov.Application.Commercial;

public enum ModuleStatus
{
    Contratado,
    Habilitado,
    Disponivel,
    EmImplantacao,
    Bloqueado,
    Beta
}

public sealed record ModuleFeatureItem(string Name, string Description);

public sealed record ModuleBenefitItem(string Title, string Description);

public sealed record ModuleKpiItem(string Name, string Value, string Hint);

public sealed record ModuleCatalogPackage(string Code, string Name, IReadOnlyList<string> ModuleCodes);

public sealed record ModuleCatalogItem(
    string Code,
    string Name,
    string Category,
    string ShortDescription,
    ModuleStatus Status,
    string Icon,
    string Route,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyList<ModuleFeatureItem> Features,
    IReadOnlyList<ModuleBenefitItem> Benefits,
    IReadOnlyList<ModuleKpiItem> Kpis,
    bool HasDemoData);

public interface IModuleCatalogService
{
    IReadOnlyList<ModuleCatalogItem> GetModules();

    ModuleCatalogItem? FindByCode(string code);

    IReadOnlyList<ModuleCatalogPackage> GetSuggestedPackages();
}
