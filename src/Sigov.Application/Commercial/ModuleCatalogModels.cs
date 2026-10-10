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

// Catálogo de NAVEGAÇÃO/COMERCIAL (estático por app). NÃO é autoridade de decisão de acesso:
// as decisões de módulo usam Sigov.Application.Saas.Modules.IModuleCatalogService (persistente,
// banco como autoridade, fail-closed). Nome próprio para desambiguar o par dual-catalog (RC-SAAS-AUT).
public interface ICommercialModuleCatalogService
{
    IReadOnlyList<ModuleCatalogItem> GetModules();

    ModuleCatalogItem? FindByCode(string code);

    IReadOnlyList<ModuleCatalogPackage> GetSuggestedPackages();
}
