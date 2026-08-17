using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Route("Tributario/Nfse")]
public sealed class TributarioNfseController:Controller { [HttpGet("{pagina?}")]public IActionResult Index(string? pagina){ViewData["Modulo"]="NFS-e, Livro Eletrônico e DES-IF preparatórios";ViewData["Pagina"]=pagina??"Dashboard";return View("~/Views/Tributario/Avancado.cshtml");}}
