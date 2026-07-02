using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class AssinaturasDigitaisController : Controller
{
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<AssinaturasDigitaisController> _logger;
    public AssinaturasDigitaisController(IAuditTrailService auditTrail, ILogger<AssinaturasDigitaisController> logger) { _auditTrail = auditTrail; _logger = logger; }
    [HttpGet("/AssinaturasDigitais")]
    [HttpGet("/AssinaturasDigitais/Solicitacoes")]
    public IActionResult Index() => View(new AssinaturasDigitaisViewModel());
    [HttpGet("/AssinaturasDigitais/Nova")] public IActionResult Nova() => View("Index", new AssinaturasDigitaisViewModel { Foco = "Nova solicitação" });
    [HttpGet("/AssinaturasDigitais/{id:long}")] public IActionResult Detalhe(long id) => View("Index", new AssinaturasDigitaisViewModel { Foco = $"Solicitação #{id}" });
    [HttpPost("/AssinaturasDigitais/Nova")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Solicitar(CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, "assinatura.solicitar", "assinatura_documento", null, null, new { status="em_implantacao" }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); TempData["Warning"] = "Assinatura eletrônica interna em implantação; não há simulação de validade ICP-Brasil."; }
        catch(Exception ex) { _logger.LogWarning(ex, "Falha auditando assinatura digital"); TempData["Error"] = "Solicitação não persistida; auditoria indisponível."; }
        return RedirectToAction(nameof(Index));
    }
}

public sealed class AssinaturasDigitaisViewModel { public string Foco { get; set; } = "Solicitações"; public string[] Status { get; set; } = new[] { "Rascunho", "Enviada", "Assinada parcialmente", "Assinada", "Recusada", "Expirada", "Cancelada" }; }
