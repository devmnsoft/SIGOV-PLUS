using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Route("PortalServidor")]
public sealed class PortalServidorController : Controller
{
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public IActionResult Dashboard() => View();
    [HttpGet("MeusDados")] public IActionResult MeusDados() => View("Area", "Meus dados");
    [HttpGet("Contracheques")] public IActionResult Contracheques() => View("Area", "Contracheques");
    [HttpGet("InformeRendimentos")] public IActionResult InformeRendimentos() => View("Area", "Informe de rendimentos");
    [HttpGet("Ferias")] public IActionResult Ferias() => View("Area", "Férias");
    [HttpGet("Requerimentos")] public IActionResult Requerimentos() => View("Area", "Requerimentos");
    [HttpGet("Documentos")] public IActionResult Documentos() => View("Area", "Documentos");
    [HttpGet("Seguranca")] public IActionResult Seguranca() => View("Area", "Segurança e acessos");
}
