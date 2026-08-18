using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Farmacia")] public sealed class SaudeFarmaciaAvancadaController:Controller{
[HttpGet("Estoque")]public IActionResult Estoque(){ViewData["Modulo"]="Farmacia";ViewData["Pagina"]="Estoque";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Lotes")]public IActionResult Lotes(){ViewData["Modulo"]="Farmacia";ViewData["Pagina"]="Lotes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Dispensacoes")]public IActionResult Dispensacoes(){ViewData["Modulo"]="Farmacia";ViewData["Pagina"]="Dispensacoes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Alertas")]public IActionResult Alertas(){ViewData["Modulo"]="Farmacia";ViewData["Pagina"]="Alertas";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Relatorios")]public IActionResult Relatorios(){ViewData["Modulo"]="Farmacia";ViewData["Pagina"]="Relatorios";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
