using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Vacinacao")] public sealed class SaudeVacinacaoController:Controller{
[HttpGet("Calendario")]public IActionResult Calendario(){ViewData["Modulo"]="Vacinacao";ViewData["Pagina"]="Calendario";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Eventos")]public IActionResult Eventos(){ViewData["Modulo"]="Vacinacao";ViewData["Pagina"]="Eventos";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Pendencias")]public IActionResult Pendencias(){ViewData["Modulo"]="Vacinacao";ViewData["Pagina"]="Pendencias";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Carteira")]public IActionResult Carteira(){ViewData["Modulo"]="Vacinacao";ViewData["Pagina"]="Carteira";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
