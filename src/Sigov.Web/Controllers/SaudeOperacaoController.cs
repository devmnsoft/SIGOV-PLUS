using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/Operacao")] public sealed class SaudeOperacaoController:Controller{
[HttpGet("Dashboard")]public IActionResult Dashboard(){ViewData["Modulo"]="Operacao";ViewData["Pagina"]="Dashboard";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
