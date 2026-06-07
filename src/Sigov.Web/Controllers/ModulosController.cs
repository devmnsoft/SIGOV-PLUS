using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;

namespace Sigov.Web.Controllers;

public sealed class ModulosController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public ModulosController(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    public IActionResult Index() => View(_moduleCatalogService.GetModules());

    public IActionResult Detalhe(string id)
    {
        var module = _moduleCatalogService.FindByCode(id);
        return module is null ? NotFound() : View(module);
    }
}
