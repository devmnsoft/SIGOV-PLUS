using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Route("Tributario/CarnesBoletos")]
public sealed class TributarioCarnesBoletosController:Controller { [HttpGet("{pagina?}")]public IActionResult Index(string? pagina){ViewData["Modulo"]="Carnês, boletos e DAM";ViewData["Pagina"]=pagina??"Dashboard";return View("~/Views/Tributario/Avancado.cshtml");}}
