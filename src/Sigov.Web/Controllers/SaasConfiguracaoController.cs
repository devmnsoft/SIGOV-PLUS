using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Application.Saas.Modules;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaasConfiguracaoController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public SaasConfiguracaoController(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    public IActionResult Modulos()
    {
        ViewData["Title"] = "Módulos e Pacotes SaaS";
        ViewData["Packages"] = _moduleCatalogService.GetPackages();
        return View(_moduleCatalogService.GetModules());
    }

    public IActionResult Perfis()
    {
        ViewData["Title"] = "Perfis e Níveis";
        return View();
    }

    public IActionResult Parametros()
    {
        ViewData["Title"] = "Parâmetros do Tenant";
        return View();
    }

    public IActionResult ContextoGlobal()
    {
        ViewData["Title"] = "Contexto Global";
        return View();
    }
}
