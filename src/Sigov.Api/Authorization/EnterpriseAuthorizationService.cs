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
    private readonly Sigov.Application.Authorization.IAuthorizationEvaluator _evaluator;
    private readonly Sigov.Application.Abstractions.ICurrentTenant _tenant;

    public EnterpriseAuthorizationHandler(Sigov.Application.Authorization.IAuthorizationEvaluator evaluator, Sigov.Application.Abstractions.ICurrentTenant tenant)
        => (_evaluator, _tenant) = (evaluator, tenant);

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EnterpriseAuthorizationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !_tenant.TenantId.HasValue) return;
        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("usuario_id") ?? context.User.FindFirstValue("user_id");
        if (!long.TryParse(rawUserId, out var userId)) return;
        var separator = requirement.Permission.LastIndexOf('.');
        var resource = separator > 0 ? requirement.Permission[..separator] : requirement.Permission;
        var action = separator > 0 ? requirement.Permission[(separator + 1)..] : "acessar";
        var decision = await _evaluator.EvaluateAsync(new Sigov.Application.Authorization.AuthorizationRequest(
            userId, resource, action, _tenant.TenantId, _tenant.EntidadeId, _tenant.ExercicioId)).ConfigureAwait(false);
        if (decision.Permitido) context.Succeed(requirement);
    }
}
