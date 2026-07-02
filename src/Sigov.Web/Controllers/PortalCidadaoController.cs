using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class PortalCidadaoController : Controller
{
 private readonly SectorModuleService _service; public PortalCidadaoController(SectorModuleService service)=>_service=service;
 [Route("/PortalCidadao")][Route("/PortalCidadao/{pagina}")] public async Task<IActionResult> Index(string? pagina,string? q,CancellationToken cancellationToken)=>View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("Portal do Cidadão", $"Portal do Cidadão{(pagina is null?"":" — "+pagina)}", "Catálogo de serviços, solicitações, protocolo e manifestações com exposição mínima de dados.", new[]{"Serviços","Solicitações","Manifestações","Protocolos"}, new[]{"/PortalCidadao/Servicos","/PortalCidadao/Solicitacoes","/PortalCidadao/NovaSolicitacao"}, false, q, cancellationToken));
 [HttpPost("/PortalCidadao/NovaSolicitacao")][ValidateAntiForgeryToken] public async Task<IActionResult> NovaSolicitacao(CancellationToken cancellationToken){ await _service.AuditAsync("portal.solicitacao.tentativa", "portal_solicitacao", cancellationToken); TempData["Warning"]="Solicitação real depende da tabela sigov.portal_solicitacao; nenhum protocolo foi simulado."; return RedirectToAction(nameof(Index)); }
}
