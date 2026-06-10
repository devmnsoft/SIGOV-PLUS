using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;
using Sigov.Application.Enterprise;

namespace Sigov.Web.Controllers;

public sealed class ComercialController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;
    private readonly IEnterpriseModuleService _enterpriseModuleService;

    public ComercialController(IModuleCatalogService moduleCatalogService, IEnterpriseModuleService enterpriseModuleService)
    {
        _moduleCatalogService = moduleCatalogService;
        _enterpriseModuleService = enterpriseModuleService;
    }

    public IActionResult Index() => View(_moduleCatalogService.GetModules());

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
}
