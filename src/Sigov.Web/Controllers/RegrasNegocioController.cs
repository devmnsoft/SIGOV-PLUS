using Microsoft.AspNetCore.Mvc;
using Sigov.Application.BusinessRules;

namespace Sigov.Web.Controllers;

public sealed class RegrasNegocioController : Controller
{
    private readonly IBusinessRuleCatalog _catalog;

    public RegrasNegocioController(IBusinessRuleCatalog catalog) => _catalog = catalog;

    public IActionResult Index() => View(_catalog.GetRules());
}
