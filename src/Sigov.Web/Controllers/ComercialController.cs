using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;

namespace Sigov.Web.Controllers;

public sealed class ComercialController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public ComercialController(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    public IActionResult Index() => View(_moduleCatalogService.GetModules());
}
