using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TecnicoController : Controller
{
    [HttpGet("/Tecnico"), HttpGet("/Tecnico/MinhaAgenda")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult MinhaAgenda() => View("~/Views/Tecnico/MinhaAgenda.cshtml");

    [HttpGet("/Tecnico/Ordens")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult Ordens() => View("~/Views/Tecnico/Ordens/Index.cshtml");

    [HttpGet("/Tecnico/Ordens/{id:guid}"), HttpGet("/Tecnico/Ordens/{id:guid}/Execucao")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult Execucao(Guid id) => View("~/Views/Tecnico/Ordens/Detalhe.cshtml", id);
}
