using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Regulacao")] public sealed class SaudeRegulacaoAvancadaController:Controller{
[HttpGet("Fila")]public IActionResult Fila(){ViewData["Modulo"]="Regulacao";ViewData["Pagina"]="Fila";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Solicitacoes")]public IActionResult Solicitacoes(){ViewData["Modulo"]="Regulacao";ViewData["Pagina"]="Solicitacoes";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("SolicitacaoDetalhe")]public IActionResult SolicitacaoDetalhe(){ViewData["Modulo"]="Regulacao";ViewData["Pagina"]="SolicitacaoDetalhe";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Relatorios")]public IActionResult Relatorios(){ViewData["Modulo"]="Regulacao";ViewData["Pagina"]="Relatorios";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
