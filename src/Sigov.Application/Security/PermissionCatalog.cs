using System.Security.Claims;

namespace Sigov.Application.Security;

/// <summary>
/// Canonical definition of permissions exposed by SIGOV applications.
/// API and Web must register authorization policies exclusively from this catalog.
/// </summary>
public sealed record PermissionDefinition(
    string Code,
    string Module,
    string Resource,
    string Action,
    string Description,
    string MenuGroup,
    bool IsAdministrative = false,
    params string[] Aliases);

public static class PermissionCatalog
{
    private const string PurchasingModule = "compras_empresariais";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        Permission("comercial.dashboard.visualizar", "comercial", "dashboard", "visualizar"),
        Permission("comercial.clientes.visualizar", "comercial", "clientes", "visualizar"),
        Permission("comercial.clientes.criar", "comercial", "clientes", "criar"),
        Permission("comercial.leads.visualizar", "comercial", "leads", "visualizar"),
        Permission("comercial.leads.criar", "comercial", "leads", "criar"),
        Permission("comercial.leads.converter", "comercial", "leads", "converter"),
        Permission("comercial.oportunidades.visualizar", "comercial", "oportunidades", "visualizar"),
        Permission("comercial.oportunidades.editar", "comercial", "oportunidades", "editar"),
        Permission("comercial.propostas.visualizar", "comercial", "propostas", "visualizar"),
        Permission("comercial.propostas.criar", "comercial", "propostas", "criar"),
        Permission("comercial.propostas.emitir", "comercial", "propostas", "emitir"),
        Permission("comercial.propostas.aprovar", "comercial", "propostas", "aprovar"),
        Permission("comercial.pedidos.visualizar", "comercial", "pedidos", "visualizar"),
        Permission("comercial.pedidos.criar", "comercial", "pedidos", "criar"),
        Permission("comercial.pedidos.confirmar", "comercial", "pedidos", "confirmar"),

        Permission("os.dashboard.visualizar", "os", "dashboard", "visualizar"),
        Permission("os.ordens.visualizar", "os", "ordens", "visualizar"),
        Permission("os.ordens.criar", "os", "ordens", "criar"),
        Permission("os.ordens.agendar", "os", "ordens", "agendar"),
        Permission("os.ordens.atribuir", "os", "ordens", "atribuir"),
        Permission("os.ordens.iniciar", "os", "ordens", "iniciar"),
        Permission("os.ordens.pausar", "os", "ordens", "pausar"),
        Permission("os.ordens.concluir", "os", "ordens", "concluir"),
        Permission("os.ordens.cancelar", "os", "ordens", "cancelar"),
        Permission("os.checklist.visualizar", "os", "checklist", "visualizar"),
        Permission("os.checklist.responder", "os", "checklist", "responder"),
        Permission("os.apontamentos.visualizar", "os", "apontamentos", "visualizar"),
        Permission("os.apontamentos.criar", "os", "apontamentos", "criar"),
        Permission("os.pecas.visualizar", "os", "pecas", "visualizar"),
        Permission("os.pecas.consumir", "os", "pecas", "consumir"),
        Permission("os.pecas.devolver", "os", "pecas", "devolver"),

        Purchasing("dashboard", "visualizar"),
        Purchasing("fornecedores", "visualizar", "com.meuerp.compras.fornecedor.visualizar"),
        Purchasing("fornecedores", "criar", "com.meuerp.compras.fornecedor.criar"),
        Purchasing("fornecedores", "editar", "com.meuerp.compras.fornecedor.editar"),
        Purchasing("fornecedores", "inativar", "com.meuerp.compras.fornecedor.inativar"),
        Purchasing("fornecedores", "homologar", "com.meuerp.compras.fornecedor.homologar"),
        Purchasing("fornecedores", "bloquear"),
        Purchasing("requisicoes", "visualizar"),
        Purchasing("requisicoes", "criar"),
        Purchasing("requisicoes", "editar"),
        Purchasing("requisicoes", "enviar"),
        Purchasing("requisicoes", "aprovar"),
        Purchasing("requisicoes", "rejeitar"),
        Purchasing("requisicoes", "cancelar"),
        Purchasing("aprovacoes", "visualizar"),
        Purchasing("aprovacoes", "aprovar"),
        Purchasing("cotacoes", "visualizar"),
        Purchasing("cotacoes", "criar"),
        Purchasing("cotacoes", "enviar"),
        Purchasing("cotacoes", "responder"),
        Purchasing("cotacoes", "julgar"),
        Purchasing("cotacoes", "cancelar"),
        Purchasing("pedidos", "visualizar"),
        Purchasing("pedidos", "gerar"),
        Purchasing("pedidos", "emitir"),
        Purchasing("pedidos", "cancelar"),
        Purchasing("pedidos", "receber"),
        Purchasing("recebimentos", "visualizar"),
        Purchasing("recebimentos", "registrar"),
        Purchasing("recebimentos", "aceitar"),
        Purchasing("recebimentos", "rejeitar"),
        Purchasing("recebimentos", "estornar"),
        Purchasing("faturas", "visualizar"),
        Purchasing("devolucoes", "visualizar"),
        Purchasing("avaliacoes", "gerenciar"),
        Purchasing("relatorios", "visualizar"),
        Purchasing("configuracao", "gerenciar", isAdministrative: true),
        Permission("WORKFLOW_CONSULTA", "workflow", "workflow", "consultar"),
        Permission("WORKFLOW_GERENCIAR", "workflow", "workflow", "gerenciar", isAdministrative: true),
        Permission("FORMULARIO_CONSULTA", "formularios", "formulario", "consultar"),
        Permission("FORMULARIO_GERENCIAR", "formularios", "formulario", "gerenciar", isAdministrative: true),
        Permission("PORTAL_CONFIGURAR", "portal", "configuracao", "gerenciar", isAdministrative: true),
        Permission("PORTAL_CONSULTAR_SOLICITACOES", "portal", "solicitacoes", "consultar"),
        Permission("SLA_CONSULTA", "sla", "regras", "consultar"),
        Permission("SLA_GERENCIAR", "sla", "regras", "gerenciar", isAdministrative: true),
        Permission("APROVACAO_CONSULTA", "aprovacoes", "aprovacao", "consultar"),
        Permission("APROVACAO_DECIDIR", "aprovacoes", "aprovacao", "decidir"),
        Permission("TEMPLATE_CONSULTA", "templates", "template", "consultar"),
        Permission("TEMPLATE_GERENCIAR", "templates", "template", "gerenciar", isAdministrative: true),
        Permission("ONBOARDING_CONSULTA", "onboarding", "checklist", "consultar"),
        Permission("SHOWCASE_ACESSAR", "showcase", "showcase", "acessar", isAdministrative: true),
        Permission("RELATORIO_EXECUTIVO", "relatorios", "executivo", "consultar", isAdministrative: true)
    ];

    public static IEnumerable<(string Policy, PermissionDefinition Permission)> Policies =>
        All.SelectMany(permission => permission.Aliases.Prepend(permission.Code)
            .Select(policy => (policy, permission)));

    public static bool UserHasPermission(ClaimsPrincipal user, PermissionDefinition permission)
    {
        if (user.IsInRole("ADMIN_GERAL") || user.IsInRole("ADMIN_TENANT"))
            return true;

        var accepted = permission.Aliases.Prepend(permission.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return user.Claims
            .Where(claim => claim.Type is "permission" or "permissions" or "scope")
            .SelectMany(claim => claim.Value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(accepted.Contains);
    }

    private static PermissionDefinition Purchasing(string resource, string action, string? alias = null, bool isAdministrative = false) =>
        Permission($"{PurchasingModule}.{resource}.{action}", PurchasingModule, resource, action, alias, isAdministrative);,

    private static PermissionDefinition Permission(string code, string module, string resource, string action, string? alias = null, bool isAdministrative = false) =>
        new(code, module, resource, action, $"Permite {action} {resource}.", module, isAdministrative,
            string.IsNullOrWhiteSpace(alias) ? [] : [alias]);
}
