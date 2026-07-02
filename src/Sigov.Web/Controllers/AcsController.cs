using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class AcsController : Controller
{
 private readonly SectorModuleService _service; public AcsController(SectorModuleService service)=>_service=service;
 [Route("/Acs")][Route("/Acs/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("ACS", $"ACS / Atenção Primária{(pagina is null?"":" — "+pagina)}", "Agentes, famílias, domicílios, visitas, mapa e sincronização offline planejada sem simulação.", new[]{"Agentes","Famílias","Domicílios","Visitas"}, new[]{"/Acs/Agentes","/Acs/Familias","/Acs/Domicilios","/Acs/Visitas","/Acs/Mapa","/Acs/Sincronizacao"}, true, q, cancellationToken));
}
