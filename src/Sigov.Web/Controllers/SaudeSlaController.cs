using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Sla")] public sealed class SaudeSlaController:Controller{
[HttpGet("Metricas")]public IActionResult Metricas(){ViewData["Modulo"]="Sla";ViewData["Pagina"]="Metricas";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
