using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class GisController : Controller
{
 private readonly SectorModuleService _service; public GisController(SectorModuleService service)=>_service=service;
 [Route("/Gis")][Route("/Gis/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("GIS", $"GIS / Georreferenciamento{(pagina is null?"":" — "+pagina)}", "Camadas, geometrias e mapa territorial preparado para operação de campo.", new[]{"Camadas","Geometrias","Pontos","Pendências"}, new[]{"/Gis/Camadas","/Gis/Mapa"}, false, q, cancellationToken));
}
