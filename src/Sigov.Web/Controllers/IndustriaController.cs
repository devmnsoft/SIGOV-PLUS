using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class IndustriaController : Controller
{
    private readonly IMenuPermissionService _modules;
    private readonly IUserPermissionService _permissions;

    public IndustriaController(IMenuPermissionService modules, IUserPermissionService permissions) =>
        (_modules, _permissions) = (modules, permissions);

    public IActionResult Dashboard() => IndustriaPage("Dashboard Industrial", "industria.dashboard.visualizar", "/api/industria/dashboard");
    public IActionResult CentrosTrabalho() => IndustriaPage("Centros de Trabalho", "industria.centros.visualizar", "/api/industria/centros-trabalho");
    public IActionResult Recursos() => IndustriaPage("Recursos Produtivos", "industria.recursos.visualizar", "/api/industria/recursos");
    public IActionResult Produtos() => IndustriaPage("Produtos Industriais", "industria.produtos.visualizar", "/api/industria/produtos");
    public IActionResult FichasTecnicas() => IndustriaPage("Ficha Técnica / BOM", "industria.fichas.visualizar", "/api/industria/fichas-tecnicas");
    public IActionResult Roteiros() => IndustriaPage("Roteiros de Produção", "industria.roteiros.visualizar", "/api/industria/roteiros");
    public IActionResult OrdensProducao() => IndustriaPage("Ordens de Produção", "industria.ordens.visualizar", "/api/industria/ordens-producao");
    public IActionResult Apontamentos() => IndustriaPage("Apontamentos de Produção", "industria.ordens.visualizar", "/api/industria/ordens-producao");
    public IActionResult Qualidade() => IndustriaPage("Controle de Qualidade", "industria.qualidade.visualizar", "/api/industria/qualidade/inspecoes");
    public IActionResult Paradas() => IndustriaPage("Paradas Produtivas", "industria.paradas.visualizar", "/api/industria/paradas");
    public IActionResult Custos() => IndustriaPage("Custos Industriais", "industria.custos.visualizar", "/api/industria/custos");
    public IActionResult ChaoFabrica() => IndustriaPage("Chão de Fábrica", "industria.chao_fabrica.acessar", "/api/industria/ordens-producao");

    private IActionResult IndustriaPage(string title, string permission, string apiRoute)
    {
        if (!_modules.CanSeeModule(User, "industria_producao") || !_permissions.HasPermission(User, permission))
            return Forbid();
        if (!long.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId) || tenantId <= 0)
            return Forbid();

        return View("ModulePage", new EnterprisePageViewModel(
            "industria_producao",
            title,
            permission,
            apiRoute,
            new EnterpriseDashboard("industria_producao", 0, 0, Array.Empty<string>(), Array.Empty<EnterpriseAuditEvent>())));
    }
}
