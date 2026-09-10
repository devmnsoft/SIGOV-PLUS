using System.Security.Claims;
using Sigov.Application.Authorization;
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
    private readonly IRequestAuthorizationSnapshot _snapshot;
    private readonly IUserPermissionService _permissions;

    public ModuleAccessService(IModuleCatalogService catalog, IRequestAuthorizationSnapshot snapshot, IUserPermissionService permissions)
    {
        _catalog = catalog;
        _snapshot = snapshot;
        _permissions = permissions;
    }

    public bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Any(PerfilNivelCodigos.GlobalAdminAliases.Contains) ||
        user.FindAll("perfil").Select(claim => claim.Value).Any(PerfilNivelCodigos.GlobalAdminAliases.Contains) ||
        _snapshot.Current.Roles.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);

    public bool CanAccess(ClaimsPrincipal user, ModuleCatalogItem module)
    {
        if (user.Identity?.IsAuthenticated != true) return false;
        var hasTenantContext = _snapshot.Current.TenantId.HasValue;
        if (IsSuperAdmin(user) && !hasTenantContext) return true;
        if (module.Status == ModuleStatus.Bloqueado) return false;
        return _snapshot.Current.HasModule(module.Code) &&
               module.RequiredPermissions.Any(permission => _permissions.HasPermission(user, permission));
    }

    public bool CanSeeModule(ClaimsPrincipal user, string moduleCode) =>
        user.Identity?.IsAuthenticated == true &&
        (IsSuperAdmin(user) && !_snapshot.Current.TenantId.HasValue || _snapshot.Current.HasModule(moduleCode));

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
