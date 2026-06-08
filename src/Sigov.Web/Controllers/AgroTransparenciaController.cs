using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroTransparenciaController : Controller
{
    public IActionResult Index() => View("~/Views/Agro/Transparencia.cshtml");
    public IActionResult Datasets() => View("~/Views/Agro/Datasets.cshtml");
    public IActionResult DicionarioDados() => View("~/Views/Agro/DicionarioDados.cshtml");
}
