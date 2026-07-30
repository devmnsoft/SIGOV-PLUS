using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers.OrdemServico;
[Authorize]
public sealed class OrdemServicoController : Controller
{
    [HttpGet("/OrdemServico"),HttpGet("/OrdemServico/Ordens")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult Ordens()=>View("~/Views/OrdemServico/Ordens/Index.cshtml");
    [HttpGet("/OrdemServico/Agenda")]
    [Authorize(Policy="os.ordens.agendar")]
    public IActionResult Agenda()=>View("~/Views/OrdemServico/Agenda/Index.cshtml");
    [HttpGet("/OrdemServico/Dashboard")]
    [Authorize(Policy="os.dashboard.visualizar")]
    public IActionResult Dashboard()=>View("~/Views/OrdemServico/Dashboard/Index.cshtml");
}
