using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;
using Sigov.Application.Enterprise;

using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class ComercialController : Controller
{
    private readonly OperationalDemoService _operationalDemo;
    private readonly ILogger<ComercialController> _operationalLogger;

    private readonly IModuleCatalogService _moduleCatalogService;
    private readonly IEnterpriseModuleService _enterpriseModuleService;

    public ComercialController(
        OperationalDemoService operationalDemo,
        ILogger<ComercialController> operationalLogger,
        IModuleCatalogService moduleCatalogService,
        IEnterpriseModuleService enterpriseModuleService)
    {
        _operationalDemo = operationalDemo;
        _operationalLogger = operationalLogger;
        _moduleCatalogService = moduleCatalogService;
        _enterpriseModuleService = enterpriseModuleService;
    }

    [Route("/Comercial")]
    public IActionResult Index(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Dashboard", q));

    public IActionResult Dashboard() => EnterprisePage("Dashboard Comercial", "/api/enterprise/comercial/dashboard", "comercial.dashboard.visualizar");

    [Route("/Comercial/Clientes")]
    public IActionResult Clientes(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Clientes", q));

    [Route("/Comercial/Leads")]
    public IActionResult Leads(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Leads", q));

    public IActionResult Oportunidades() => EnterprisePage("Oportunidades", "/api/comercial/oportunidades", "comercial.oportunidades.visualizar");

    [Route("/Comercial/Propostas")]
    public IActionResult Propostas(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Propostas", q));

    [Route("/Comercial/Pedidos")]
    public IActionResult Pedidos(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Pedidos", q));

    public IActionResult TabelasPreco() => EnterprisePage("Tabelas de Preço", "/api/comercial/tabelas-preco", "comercial.tabelas_preco.visualizar");

    private IActionResult EnterprisePage(string title, string apiRoute, string permission)
    {
        var dashboard = _enterpriseModuleService.GetDashboard("comercial", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        return View("~/Views/Enterprise/ModulePage.cshtml", new Sigov.Web.Controllers.EnterprisePageViewModel("comercial", title, permission, apiRoute, dashboard));
    }


    [Route("/Comercial/Funil")]
    public IActionResult Funil(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Funil", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Comercial/Funil");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Em implantação"));
        }
    }
}
