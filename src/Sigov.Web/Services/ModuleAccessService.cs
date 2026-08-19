using System.Security.Claims;
using Sigov.Application.Commercial;
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
    private readonly IUserPermissionService _permissions;

    public ModuleAccessService(IModuleCatalogService catalog, IUserPermissionService permissions)
    {
        _catalog = catalog;
        _permissions = permissions;
    }

    public bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole("SUPERADMIN") || user.IsInRole("ADMIN_GERAL") ||
        user.HasClaim("perfil", "SUPERADMIN") || user.HasClaim("perfil", "ADMIN_GERAL");

    public bool CanAccess(ClaimsPrincipal user, ModuleCatalogItem module)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        if (IsSuperAdmin(user)) return true;
        if (module.Status == ModuleStatus.Bloqueado) return false;

        var moduleEnabled = user.Claims.Any(c =>
            (c.Type is "module" or "modulo" or "modules") &&
            string.Equals(c.Value, module.Code, StringComparison.OrdinalIgnoreCase));
        return moduleEnabled || module.RequiredPermissions.Any(permission => _permissions.HasPermission(user, permission));
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
