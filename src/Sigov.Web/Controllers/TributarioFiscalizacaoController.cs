using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Route("Tributario/Fiscalizacao")]
public sealed class TributarioFiscalizacaoController:Controller { [HttpGet("{pagina?}")]public IActionResult Index(string? pagina){ViewData["Modulo"]="Fiscalização ISSQN";ViewData["Pagina"]=pagina??"Dashboard";return View("~/Views/Tributario/Avancado.cshtml");}}
