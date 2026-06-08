using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class PlanosController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Detalhe(string codigo) { ViewData["Codigo"] = codigo; return View(); }
}
