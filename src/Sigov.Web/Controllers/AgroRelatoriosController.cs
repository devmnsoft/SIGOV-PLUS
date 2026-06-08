using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroRelatoriosController : Controller
{
    public IActionResult Index() => View("~/Views/Agro/Relatorios.cshtml");
    public IActionResult Executar(long id) { ViewData["ModeloId"] = id; return View("~/Views/Agro/ExecutarRelatorio.cshtml"); }
}
