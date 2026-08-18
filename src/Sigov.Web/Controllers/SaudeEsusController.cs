using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Esus")] public sealed class SaudeEsusController:Controller{
[HttpGet("Lotes")]public IActionResult Lotes(){ViewData["Modulo"]="Esus";ViewData["Pagina"]="Lotes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Inconsistencias")]public IActionResult Inconsistencias(){ViewData["Modulo"]="Esus";ViewData["Pagina"]="Inconsistencias";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
