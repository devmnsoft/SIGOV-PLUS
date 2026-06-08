using Sigov.Application.Saas.Modules;

namespace Sigov.Infrastructure.Saas;

public sealed class ModuleCatalogRepository
{
    private readonly IModuleCatalogService _catalogService;

    public ModuleCatalogRepository(IModuleCatalogService catalogService) => _catalogService = catalogService;

    public IReadOnlyCollection<ModuleCatalogItem> GetModules() => _catalogService.GetModules();
}
