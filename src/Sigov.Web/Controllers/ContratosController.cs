using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class ContratosController : Controller
{
    private readonly OperationalDemoService _demo;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<ContratosController> _logger;
    public ContratosController(OperationalDemoService demo, IAuditTrailService auditTrail, ILogger<ContratosController> logger) { _demo = demo; _auditTrail = auditTrail; _logger = logger; }
    [Route("/Contratos")]
    [Route("/Contratos/Listar")]
    [Route("/Contratos/Novo")]
    [Route("/Contratos/Vencimentos")]
    public IActionResult Index(string? q = null) => View("~/Views/Operational/Module.cshtml", _demo.Build("Contratos", RouteData.Values["action"]?.ToString() ?? "Dashboard", q));
    [Route("/Contratos/Detalhes/{id:long}")]
    public IActionResult Detalhes(long id) => View("~/Views/Operational/Module.cshtml", _demo.Build("Contratos", $"Detalhes #{id}"));
    [HttpPost, ValidateAntiForgeryToken, Route("/Contratos/Novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken) { await Audit("contrato.criar", null, cancellationToken); TempData["Warning"] = "Contrato não foi salvo porque o schema real ainda precisa estar homologado."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Contratos/{id:long}/Arquivar")]
    public async Task<IActionResult> Arquivar(long id, CancellationToken cancellationToken) { await Audit("contrato.arquivar", id.ToString(), cancellationToken); TempData["Info"] = "Arquivamento registrado para auditoria quando tabela existir; sem simular persistência."; return Redirect($"/Contratos/Detalhes/{id}"); }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "contrato", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Auditoria operacional falhou"); } }
}
