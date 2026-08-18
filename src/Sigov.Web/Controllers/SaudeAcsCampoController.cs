using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize,Route("Saude/AcsCampo")] public sealed class SaudeAcsCampoController:Controller{
[HttpGet("Dashboard")]public IActionResult Dashboard(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Dashboard";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Domicilios")]public IActionResult Domicilios(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Domicilios";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("DomicilioDetalhe")]public IActionResult DomicilioDetalhe(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="DomicilioDetalhe";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("CadastrosIndividuais")]public IActionResult CadastrosIndividuais(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="CadastrosIndividuais";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Visitas")]public IActionResult Visitas(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Visitas";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("AtividadesColetivas")]public IActionResult AtividadesColetivas(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="AtividadesColetivas";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("ConsumoAlimentar")]public IActionResult ConsumoAlimentar(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="ConsumoAlimentar";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Ocorrencias")]public IActionResult Ocorrencias(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Ocorrencias";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Dengue")]public IActionResult Dengue(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Dengue";return View("~/Views/Saude/Avancada/Painel.cshtml");}
[HttpGet("Relatorios")]public IActionResult Relatorios(){ViewData["Modulo"]="AcsCampo";ViewData["Pagina"]="Relatorios";return View("~/Views/Saude/Avancada/Painel.cshtml");}
}
