using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Sigov.Api.Authorization;

public sealed record EnterpriseAuthorizationRequirement(Guid TenantId, string Permission)
    : IAuthorizationRequirement;

public interface IEnterpriseAuthorizationService
{
    string ResolveRequiredPermission(HttpRequest request, RouteData routeData);
    Task<bool> AuthorizeAsync(ClaimsPrincipal user, Guid tenantId, string permission, CancellationToken cancellationToken);
}

public sealed class EnterpriseAuthorizationService : IEnterpriseAuthorizationService
{
    private readonly IAuthorizationService _authorization;

    public EnterpriseAuthorizationService(IAuthorizationService authorization) => _authorization = authorization;

    public string ResolveRequiredPermission(HttpRequest request, RouteData routeData)
    {
        var area = (routeData.Values["area"]?.ToString() ?? string.Empty).Trim('/').ToLowerInvariant();
        var path = request.Path.Value ?? string.Empty;
        var action = ResolveAction(path, request.Method);

        if (action == "CSV") return "enterprise.relatorios.exportar";
        if (area is "clientes" or "comercial/clientes") return ForCrud("comercial.clientes", action);
        if (area.Contains("propostas", StringComparison.Ordinal)) return action switch
        {
            "APROVAR" or "GERAR_PEDIDO" => "comercial.propostas.aprovar",
            "REPROVAR" => "comercial.propostas.reprovar",
            "GET" => "comercial.propostas.visualizar",
            _ => "comercial.propostas.criar"
        };
        if (area.Contains("pedidos", StringComparison.Ordinal)) return action switch
        {
            "CONFIRMAR" or "GERAR_OS" => "comercial.pedidos.confirmar",
            "CANCELAR" => "comercial.pedidos.cancelar",
            _ => "comercial.pedidos.visualizar"
        };
        if (area.Contains("produtos", StringComparison.Ordinal)) return ForCrud("estoque.produtos", action);
        if (area.Contains("fornecedores", StringComparison.Ordinal)) return ForCrud("compras.fornecedores", action);
        if (area.Contains("ativos", StringComparison.Ordinal)) return ForCrud("industrial.ativos", action);
        if (area.Contains("ordens", StringComparison.Ordinal)) return action switch
        {
            "INICIAR" => "os.ordens.iniciar",
            "CONCLUIR" => "os.ordens.concluir",
            "CANCELAR" => "os.ordens.cancelar",
            "AGENDAR" => "os.ordens.agendar",
            "PAUSAR" => "os.ordens.pausar",
            "GET" => "os.ordens.visualizar",
            _ => "os.ordens.criar"
        };

        // Unknown Enterprise resources are denied unless an explicit, resource-scoped
        // permission is granted; never fall back to generic authentication.
        var resource = string.IsNullOrWhiteSpace(area) ? "enterprise" : $"enterprise.{area.Replace('/', '.')}";
        return ForCrud(resource, action);
    }

    public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        var result = await _authorization.AuthorizeAsync(
            user,
            resource: null,
            new EnterpriseAuthorizationRequirement(tenantId, permission)).ConfigureAwait(false);
        return result.Succeeded;
    }

    private static string ForCrud(string resource, string action) => action switch
    {
        "POST" => $"{resource}.criar",
        "PUT" or "PATCH" or "RESTAURAR" => $"{resource}.editar",
        "DELETE" => $"{resource}.inativar",
        _ => $"{resource}.visualizar"
    };

    private static string ResolveAction(string path, string method)
    {
        if (path.Contains("export-csv", StringComparison.OrdinalIgnoreCase)) return "CSV";
        foreach (var action in new[] { "aprovar", "reprovar", "confirmar", "cancelar", "iniciar", "concluir", "agendar", "pausar", "restaurar", "gerar-pedido", "gerar-os", "entrada", "saida", "ajuste" })
            if (path.EndsWith('/' + action, StringComparison.OrdinalIgnoreCase) || path.Contains('/' + action + '/', StringComparison.OrdinalIgnoreCase))
                return action.ToUpperInvariant().Replace('-', '_');
        return method.ToUpperInvariant();
    }
}

public sealed class EnterpriseAuthorizationHandler : AuthorizationHandler<EnterpriseAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EnterpriseAuthorizationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Task.CompletedTask;

        var globalAdmin = IsInRole(context.User, "ADMIN_GERAL");
        var tenantAdmin = IsInRole(context.User, "ADMIN_TENANT");
        if (globalAdmin)
        {
            context.Succeed(requirement); // tenant selection is explicit in the requirement.
            return Task.CompletedTask;
        }

        if (tenantAdmin)
        {
            var tenantClaim = FindTenant(context.User);
            if (tenantClaim == requirement.TenantId) context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (Permissions(context.User).Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsInRole(ClaimsPrincipal user, string role) =>
        user.IsInRole(role) || user.Claims.Any(c => (c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase)) && c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));

    private static Guid? FindTenant(ClaimsPrincipal user)
    {
        var value = user.Claims.FirstOrDefault(c => (c.Type.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("tenantId", StringComparison.OrdinalIgnoreCase) || c.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase)))?.Value;
        return Guid.TryParse(value, out var tenant) ? tenant : null;
    }

    private static IEnumerable<string> Permissions(ClaimsPrincipal user) => user.Claims
        .Where(c => c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("permissions", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("scope", StringComparison.OrdinalIgnoreCase))
        .SelectMany(c => c.Value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
