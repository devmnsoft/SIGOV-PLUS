using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class AtendimentoController : Controller
{
 private readonly SectorModuleService _service; public AtendimentoController(SectorModuleService service)=>_service=service;
 [Route("/Atendimento")][Route("/Atendimento/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("Atendimento", $"Atendimento{(pagina is null?"":" — "+pagina)}", "Central de solicitações, prazos, responsáveis e protocolo vinculado.", new[]{"Solicitações","Protocolos","Prazos","Responsáveis"}, new[]{"/Atendimento/Solicitacoes"}, true, q, cancellationToken));
}
