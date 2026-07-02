using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class NotificacoesController : Controller
{
    private readonly NotificacaoService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<NotificacoesController> _logger;
    public NotificacoesController(NotificacaoService service, IAuditTrailService auditTrail, ILogger<NotificacoesController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }
    [HttpGet("/Notificacoes")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) { try { return View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(cancellationToken)); } catch (Exception ex) { _logger.LogError(ex, "Falha notificações"); return View("~/Views/Operational/Hub.cshtml", new Sigov.Web.Models.Operational.OperationalHubViewModel { AreaKey="Notificacoes", Title="Notificações", Description="Não foi possível carregar notificações." }); } }
    [HttpPost("/Notificacoes/{id:long}/MarcarLida")][ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLida(long id, CancellationToken cancellationToken) { await AuditAsync("NOTIFICACAO_LIDA", id.ToString(), cancellationToken); TempData["Toast"]="Leitura registrada quando há schema real; sem simulação de persistência."; return RedirectToAction(nameof(Index)); }
    [HttpPost("/Notificacoes/MarcarTodasLidas")][ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarTodasLidas(CancellationToken cancellationToken) { await AuditAsync("NOTIFICACAO_TODAS_LIDAS", null, cancellationToken); TempData["Toast"]="Solicitação auditada; marcação real depende do schema sigov.notificacao_usuario."; return RedirectToAction(nameof(Index)); }
    private Task AuditAsync(string acao,string? id,CancellationToken ct)=>_auditTrail.RegistrarAsync(null,null,acao,"Notificacao",id,null,new{acao,id},HttpContext.Connection.RemoteIpAddress?.ToString(),Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct);
}
