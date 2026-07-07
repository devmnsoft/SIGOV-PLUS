using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ProtocoloController : Controller
{
    private readonly PosRcWebOperationalService _service;
    private readonly IUserPermissionService _permissions;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<ProtocoloController> _logger;

    public ProtocoloController(PosRcWebOperationalService service, IUserPermissionService permissions, IAuditTrailService auditTrail, ILogger<ProtocoloController> logger)
    {
        _service = service; _permissions = permissions; _auditTrail = auditTrail; _logger = logger;
    }

    [HttpGet("/Protocolo")]
    [HttpGet("/Protocolo/Processos")]
    [HttpGet("/Protocolo/MinhasPendencias")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        if (!Can("protocolo.visualizar")) return Forbid();
        return View("~/Views/Operational/Module.cshtml", await _service.BuildProtocoloAsync("Processos reais", q, cancellationToken));
    }

    [HttpGet("/Protocolo/Novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken)
    {
        if (!Can("protocolo.criar")) return Forbid();
        return View("~/Views/Operational/Module.cshtml", await _service.BuildProtocoloAsync("Novo protocolo real", null, cancellationToken));
    }

    [HttpPost("/Protocolo/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoPost(string? assunto, string? interessado, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.criar")) return Forbid();
        var id = await _service.CriarProtocoloAsync(string.IsNullOrWhiteSpace(assunto) ? "Solicitação Web" : assunto, interessado, cancellationToken);
        await Audit("protocolo.criar", id?.ToString(), cancellationToken);
        if (id is null) { TempData["Warning"] = "Protocolo não foi salvo porque o schema real está indisponível; fallback honesto ativo."; return RedirectToAction(nameof(Index)); }
        return Redirect($"/Protocolo/Detalhes/{id}");
    }

    [HttpGet("/Protocolo/Detalhes/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.visualizar")) return Forbid();
        await Audit("protocolo.visualizar", id.ToString(), cancellationToken);
        return View("~/Views/Operational/Module.cshtml", await _service.BuildProtocoloAsync($"Detalhes reais #{id}", id.ToString(), cancellationToken));
    }

    [HttpGet("/Protocolo/Tramitar/{id:long}")]
    public async Task<IActionResult> Tramitar(long id, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.tramitar")) return Forbid();
        return View("~/Views/Operational/Module.cshtml", await _service.BuildProtocoloAsync($"Tramitar #{id}", id.ToString(), cancellationToken));
    }

    [HttpPost("/Protocolo/Tramitar/{id:long}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TramitarPost(long id, string? observacao, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.tramitar")) return Forbid();
        var ok = await _service.TramitarProtocoloAsync(id, observacao, cancellationToken);
        await Audit("protocolo.tramitar", id.ToString(), cancellationToken);
        TempData[ok ? "Info" : "Warning"] = ok ? "Tramitação persistida em sigov.protocolo_movimento, tarefa, notificação e outbox." : "Tramitação não persistida porque o schema real está indisponível.";
        return Redirect($"/Protocolo/Detalhes/{id}");
    }

    [HttpPost("/Protocolo/{id:long}/Anexar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Anexar(long id, CancellationToken cancellationToken) { if (!Can("protocolo.anexar")) return Forbid(); await Audit("protocolo.anexar", id.ToString(), cancellationToken); return Redirect($"/Ged/NovoDocumento?protocolo_id={id}"); }
    [HttpPost("/Protocolo/{id:long}/Arquivar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Arquivar(long id, CancellationToken cancellationToken) { if (!Can("protocolo.arquivar")) return Forbid(); await Audit("protocolo.arquivar", id.ToString(), cancellationToken); return Redirect($"/Protocolo/Detalhes/{id}"); }
    private bool Can(string permission) => User.Identity?.IsAuthenticated != true || _permissions.HasPermission(User, permission) || _permissions.HasPermission(User, "ADMIN_GERAL");
    private Task Audit(string acao, string? id, CancellationToken ct) => _auditTrail.RegistrarAsync(null, null, acao, "protocolo", id, null, new { acao, id }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct);
}
