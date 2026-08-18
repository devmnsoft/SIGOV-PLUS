using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Suporte")] public sealed class SaudeSuporteController:Controller{
[HttpGet("Chamados")]public IActionResult Chamados(){ViewData["Modulo"]="Suporte";ViewData["Pagina"]="Chamados";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("ChamadoDetalhe")]public IActionResult ChamadoDetalhe(){ViewData["Modulo"]="Suporte";ViewData["Pagina"]="ChamadoDetalhe";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
