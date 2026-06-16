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

    public IActionResult Index() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Comercial", "Dashboard"));

    public IActionResult Dashboard() => EnterprisePage("Dashboard Comercial", "/api/enterprise/comercial/dashboard", "comercial.dashboard.visualizar");

    public IActionResult Clientes() => EnterprisePage("Clientes Comerciais", "/api/comercial/clientes", "comercial.clientes.visualizar");

    public IActionResult Leads() => EnterprisePage("Leads", "/api/comercial/leads", "comercial.leads.visualizar");

    public IActionResult Oportunidades() => EnterprisePage("Oportunidades", "/api/comercial/oportunidades", "comercial.oportunidades.visualizar");

    public IActionResult Propostas() => EnterprisePage("Propostas", "/api/comercial/propostas", "comercial.propostas.visualizar");

    public IActionResult Pedidos() => EnterprisePage("Pedidos Comerciais", "/api/comercial/pedidos", "comercial.pedidos.visualizar");

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
