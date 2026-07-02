using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class PortalContribuinteController : Controller
{
 private readonly SectorModuleService _service; public PortalContribuinteController(SectorModuleService service)=>_service=service;
 [Route("/PortalContribuinte")][Route("/PortalContribuinte/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("Portal do Contribuinte", $"Portal do Contribuinte{(pagina is null?"":" — "+pagina)}", "Débitos, guias, certidões futuras e protocolos sem simular emissão fiscal.", new[]{"Contribuintes","Débitos","Guias","Protocolos"}, new[]{"/PortalContribuinte/Debitos","/PortalContribuinte/Guias","/PortalContribuinte/Certidoes","/PortalContribuinte/Protocolos"}, false, q, cancellationToken));
}
