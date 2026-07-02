using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class BiSetorialController : Controller
{
 private readonly SectorModuleService _service; public BiSetorialController(SectorModuleService service)=>_service=service;
 [Route("/BiSetorial")][Route("/BiSetorial/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("BI Setorial", $"BI Setorial{(pagina is null?"":" — "+pagina)}", "Indicadores reais quando o schema existir; qualquer gráfico sem fonte fica explicitamente em demonstração.", new[]{"Educação","Saúde","Saneamento","Social/Agro"}, new[]{"/BiSetorial/Educacao","/BiSetorial/Saude","/BiSetorial/Saneamento","/BiSetorial/Social","/BiSetorial/Agro"}, true, q, cancellationToken));
}
