using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroBiController : Controller
{
    public IActionResult Index() => View("~/Views/Agro/Bi.cshtml");
    public IActionResult Indicadores() => View("~/Views/Agro/Indicadores.cshtml");
}
