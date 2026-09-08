using System.Security.Claims;
using Sigov.Application.Commercial;
using Sigov.Domain.Saas;
using Sigov.Web.Models.Security;

namespace Sigov.Web.Services;

public interface IModuloAccessService
{
    bool IsSuperAdmin(ClaimsPrincipal user);
    bool CanAccess(ClaimsPrincipal user, ModuleCatalogItem module);
    IReadOnlyList<ModuleAccessCardViewModel> BuildCatalog(ClaimsPrincipal user, bool includeBlocked);
}

public interface IMenuPermissionService
{
    bool CanSeeModule(ClaimsPrincipal user, string moduleCode);
}

public sealed class ModuleAccessService : IModuloAccessService, IMenuPermissionService
{
    private static readonly string[] SensitiveModules = { "saude", "educacao", "social", "auditoria-lgpd", "rh" };
    private readonly IModuleCatalogService _catalog;
    private readonly Sigov.Application.Saas.Modules.IModuleCatalogService _canonicalCatalog;
    private readonly IUserPermissionService _permissions;

    public ModuleAccessService(IModuleCatalogService catalog, Sigov.Application.Saas.Modules.IModuleCatalogService canonicalCatalog, IUserPermissionService permissions)
    {
        _catalog = catalog;
        _canonicalCatalog = canonicalCatalog;
        _permissions = permissions;
    }

    public bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Any(PerfilNivelCodigos.GlobalAdminAliases.Contains) ||
        user.FindAll("perfil").Select(claim => claim.Value).Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);

    public bool CanAccess(ClaimsPrincipal user, ModuleCatalogItem module)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        var hasTenantContext = long.TryParse(user.FindFirstValue("tenant_id"), out var tenantId) && tenantId > 0;
        if (IsSuperAdmin(user) && !hasTenantContext) return true;
        if (module.Status == ModuleStatus.Bloqueado) return false;

        var moduleEnabled = user.Claims.Any(c =>
            (c.Type is "module" or "modulo" or "modules") &&
            string.Equals(c.Value, module.Code, StringComparison.OrdinalIgnoreCase));
        var dependenciesEnabled = _canonicalCatalog.FindByCode(module.Code)?.Dependencias.All(dependency => user.Claims.Any(c =>
            (c.Type is "module" or "modulo" or "modules") &&
            string.Equals(c.Value, dependency, StringComparison.OrdinalIgnoreCase))) == true;
        return moduleEnabled && dependenciesEnabled && module.RequiredPermissions.Any(permission => _permissions.HasPermission(user, permission));
    }

    public bool CanSeeModule(ClaimsPrincipal user, string moduleCode)
    {
        var module = _catalog.FindByCode(moduleCode);
        return module is not null && CanAccess(user, module);
    }

    public IReadOnlyList<ModuleAccessCardViewModel> BuildCatalog(ClaimsPrincipal user, bool includeBlocked)
    {
        var superAdmin = IsSuperAdmin(user);
        return _catalog.GetModules()
            .Select(module =>
            {
                var allowed = CanAccess(user, module);
                var reason = allowed ? string.Empty : module.Status == ModuleStatus.Bloqueado
                    ? "Módulo bloqueado para operação."
                    : "Módulo não contratado, não habilitado ou sem permissão no perfil atual.";
                return new ModuleAccessCardViewModel(
                    module.Code, module.Name, module.Category, module.ShortDescription, module.Icon,
                    module.Route, module.Status.ToString(), allowed, reason,
                    SensitiveModules.Contains(module.Code, StringComparer.OrdinalIgnoreCase),
                    allowed ? module.RequiredPermissions : Array.Empty<string>());
            })
            .Where(module => module.Allowed || includeBlocked || superAdmin)
            .OrderBy(module => module.Category).ThenBy(module => module.Name)
            .ToArray();
    }
}
