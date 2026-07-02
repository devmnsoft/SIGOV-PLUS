using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

public sealed class ProtocoloController : Controller
{
    private readonly ProtocoloOperationalService _demo;
    private readonly ILogger<ProtocoloController> _logger;
    private readonly IAuditTrailService _auditTrail;
    public ProtocoloController(ProtocoloOperationalService demo, IAuditTrailService auditTrail, ILogger<ProtocoloController> logger) { _demo = demo; _auditTrail = auditTrail; _logger = logger; }
    [Route("/Protocolo")]
    [Route("/Protocolo/Processos")]
    [Route("/Protocolo/Novo")]
    [Route("/Protocolo/Tramitar")]
    [Route("/Protocolo/MinhasPendencias")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default) { try { return View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Protocolo", RouteData.Values["action"]?.ToString() ?? "Dashboard", q, cancellationToken)); } catch (Exception ex) { _logger.LogError(ex, "Falha Protocolo"); return View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Protocolo", "Dashboard", q, cancellationToken)); } }
    [Route("/Protocolo/Detalhes/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Protocolo", $"Detalhes #{id}", null, cancellationToken));
    [Route("/Protocolo/Tramitar/{id:long}")]
    public async Task<IActionResult> Tramitar(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Protocolo", $"Tramitar #{id}", null, cancellationToken));
    [HttpPost, ValidateAntiForgeryToken, Route("/Protocolo/Novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken) { await Audit("protocolo.abrir", null, cancellationToken); TempData["Warning"] = "Protocolo não foi salvo sem schema operacional homologado; sem sucesso falso."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Protocolo/Tramitar/{id:long}")]
    public async Task<IActionResult> TramitarPost(long id, CancellationToken cancellationToken) { await Audit("protocolo.tramitar", id.ToString(), cancellationToken); TempData["Info"] = "Tramitação auditada quando possível; persistência depende do schema."; return Redirect($"/Protocolo/Detalhes/{id}"); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Protocolo/{id:long}/Anexar")]
    public async Task<IActionResult> Anexar(long id, CancellationToken cancellationToken) { await Audit("protocolo.anexar", id.ToString(), cancellationToken); TempData["Warning"] = "Anexo não persistido sem tabela/armazenamento homologado."; return Redirect($"/Protocolo/Detalhes/{id}"); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Protocolo/{id:long}/Arquivar")]
    public async Task<IActionResult> Arquivar(long id, CancellationToken cancellationToken) { await Audit("protocolo.arquivar", id.ToString(), cancellationToken); return Redirect($"/Protocolo/Detalhes/{id}"); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Protocolo/{id:long}/Reabrir")]
    public async Task<IActionResult> Reabrir(long id, CancellationToken cancellationToken) { await Audit("protocolo.reabrir", id.ToString(), cancellationToken); return Redirect($"/Protocolo/Detalhes/{id}"); }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "protocolo", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Auditoria protocolo falhou"); } }
}
