using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

public sealed class ContratosController : Controller
{
    private readonly ContratosOperationalService _demo;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<ContratosController> _logger;
    public ContratosController(ContratosOperationalService demo, IAuditTrailService auditTrail, ILogger<ContratosController> logger) { _demo = demo; _auditTrail = auditTrail; _logger = logger; }
    [Route("/Contratos")]
    [Route("/Contratos/Listar")]
    [Route("/Contratos/Novo")]
    [Route("/Contratos/Vencimentos")]
    [Route("/Contratos/Aditivos")]
    [Route("/Contratos/Fiscais")]
    [Route("/Contratos/Medicoes")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Contratos", RouteData.Values["action"]?.ToString() ?? "Dashboard", q, cancellationToken));
    [Route("/Contratos/Detalhes/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _demo.BuildAsync("Contratos", $"Detalhes #{id}", null, cancellationToken));
    [HttpPost, ValidateAntiForgeryToken, Route("/Contratos/Novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken) { await Audit("contrato.criar", null, cancellationToken); TempData["Warning"] = "Contrato não foi salvo porque o schema real ainda precisa estar homologado."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Contratos/{id:long}/Arquivar")]
    public async Task<IActionResult> Arquivar(long id, CancellationToken cancellationToken) { await Audit("contrato.arquivar", id.ToString(), cancellationToken); TempData["Info"] = "Arquivamento registrado para auditoria quando tabela existir; sem simular persistência."; return Redirect($"/Contratos/Detalhes/{id}"); }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "contrato", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Auditoria operacional falhou"); } }
}
