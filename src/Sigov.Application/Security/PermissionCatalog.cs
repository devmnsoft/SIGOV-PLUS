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
        Permission("tributario.contribuinte.visualizar", "tributario", "contribuinte", "visualizar"),
        Permission("tributario.contribuinte.criar", "tributario", "contribuinte", "criar"),
        Permission("tributario.contribuinte.alterar", "tributario", "contribuinte", "alterar"),
        Permission("tributario.lancamento.visualizar", "tributario", "lancamento", "visualizar"),
        Permission("tributario.lancamento.criar", "tributario", "lancamento", "criar"),
        Permission("tributario.lancamento.cancelar", "tributario", "lancamento", "cancelar"),
        Permission("tributario.dam.emitir", "tributario", "dam", "emitir"),
        Permission("tributario.dam.cancelar", "tributario", "dam", "cancelar"),
        Permission("tributario.divida_ativa.inscrever", "tributario", "divida_ativa", "inscrever"),
        Permission("tributario.fiscalizacao.abrir", "tributario", "fiscalizacao", "abrir"),
        Permission("tributario.relatorio.exportar", "tributario", "relatorio", "exportar"),

        Permission("financeiro.dashboard.visualizar", "financeiro", "dashboard", "visualizar"),
        Permission("financeiro.arrecadacao.visualizar", "financeiro", "arrecadacao", "visualizar"),
        Permission("financeiro.arrecadacao.baixar", "financeiro", "arrecadacao", "baixar"),
        Permission("financeiro.arrecadacao.estornar", "financeiro", "arrecadacao", "estornar"),
        Permission("financeiro.pagamento.registrar", "financeiro", "pagamento", "registrar"),
        Permission("financeiro.relatorio.exportar", "financeiro", "relatorio", "exportar"),
        Permission("financeiro.configurar", "financeiro", "configuracao", "configurar", isAdministrative: true),

        Permission("saneamento.consumidor.visualizar", "saneamento", "consumidor", "visualizar"),
        Permission("saneamento.consumidor.criar", "saneamento", "consumidor", "criar"),
        Permission("saneamento.consumidor.alterar", "saneamento", "consumidor", "alterar"),
        Permission("saneamento.ligacao.criar", "saneamento", "ligacao", "criar"),
        Permission("saneamento.hidrometro.instalar", "saneamento", "hidrometro", "instalar"),
        Permission("saneamento.hidrometro.substituir", "saneamento", "hidrometro", "substituir"),
        Permission("saneamento.leitura.registrar", "saneamento", "leitura", "registrar"),
        Permission("saneamento.fatura.gerar", "saneamento", "fatura", "gerar"),
        Permission("saneamento.fatura.cancelar", "saneamento", "fatura", "cancelar"),
        Permission("saneamento.pagamento.registrar", "saneamento", "pagamento", "registrar"),
        Permission("saneamento.corte.executar", "saneamento", "corte", "executar"),
        Permission("saneamento.religacao.executar", "saneamento", "religacao", "executar"),
        Permission("saneamento.relatorio.exportar", "saneamento", "relatorio", "exportar"),

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
        Permission("RELATORIO_EXECUTIVO", "relatorios", "executivo", "consultar", isAdministrative: true),
        Permission("arrei.visualizar", "arrei", "solicitacoes", "visualizar"),
        Permission("arrei.criar", "arrei", "solicitacoes", "criar"),
        Permission("arrei.editar", "arrei", "solicitacoes", "editar"),
        Permission("arrei.analisar", "arrei", "solicitacoes", "analisar"),
        Permission("arrei.atribuir", "arrei", "solicitacoes", "atribuir"),
        Permission("arrei.deferir", "arrei", "solicitacoes", "deferir"),
        Permission("arrei.indeferir", "arrei", "solicitacoes", "indeferir"),
        Permission("arrei.configurar", "arrei", "configuracao", "gerenciar", isAdministrative: true),
        Permission("arrei.exportar", "arrei", "relatorios", "exportar"),
        Permission("arrei.portal", "arrei", "portal", "acessar")
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
        Permission($"{PurchasingModule}.{resource}.{action}", PurchasingModule, resource, action, alias, isAdministrative);

    private static PermissionDefinition Permission(string code, string module, string resource, string action, string? alias = null, bool isAdministrative = false) =>
        new(code, module, resource, action, $"Permite {action} {resource}.", module, isAdministrative,
            string.IsNullOrWhiteSpace(alias) ? [] : [alias]);
}
