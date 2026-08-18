using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Retaguarda")] public sealed class SaudeRetaguardaController:Controller{
[HttpGet("Dashboard")]public IActionResult Dashboard(){ViewData["Modulo"]="Retaguarda";ViewData["Pagina"]="Dashboard";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("ProducaoAcs")]public IActionResult ProducaoAcs(){ViewData["Modulo"]="Retaguarda";ViewData["Pagina"]="ProducaoAcs";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Mapa")]public IActionResult Mapa(){ViewData["Modulo"]="Retaguarda";ViewData["Pagina"]="Mapa";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Indicadores")]public IActionResult Indicadores(){ViewData["Modulo"]="Retaguarda";ViewData["Pagina"]="Indicadores";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
