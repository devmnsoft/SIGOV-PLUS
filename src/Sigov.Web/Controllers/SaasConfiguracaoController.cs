using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Application.Saas.Modules;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaasConfiguracaoController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public SaasConfiguracaoController(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    public async Task<IActionResult> Modulos(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Módulos e Pacotes SaaS";
        ViewData["Packages"] = await _moduleCatalogService.GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        return View(await _moduleCatalogService.GetModulesAsync(cancellationToken).ConfigureAwait(false));
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
