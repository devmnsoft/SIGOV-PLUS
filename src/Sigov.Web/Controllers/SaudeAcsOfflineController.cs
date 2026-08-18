using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/AcsOffline")] public sealed class SaudeAcsOfflineController:Controller{
[HttpGet("Dashboard")]public IActionResult Dashboard(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Dashboard";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Dispositivos")]public IActionResult Dispositivos(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Dispositivos";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Lotes")]public IActionResult Lotes(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Lotes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("LoteDetalhe")]public IActionResult LoteDetalhe(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="LoteDetalhe";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Sincronizacoes")]public IActionResult Sincronizacoes(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Sincronizacoes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Inconsistencias")]public IActionResult Inconsistencias(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Inconsistencias";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Georreferencia")]public IActionResult Georreferencia(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Georreferencia";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Producao")]public IActionResult Producao(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Producao";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Relatorios")]public IActionResult Relatorios(){ViewData["Modulo"]="AcsOffline";ViewData["Pagina"]="Relatorios";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
