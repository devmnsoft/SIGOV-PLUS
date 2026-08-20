using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Models.Protocolo;

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
    [HttpGet("/Protocolo/Meus")]
    [HttpGet("/Protocolo/Pendentes")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        if (!Can("protocolo.visualizar")) return Forbid();
        return View("~/Views/Operational/Module.cshtml", await _service.BuildProtocoloAsync("Processos reais", q, cancellationToken));
    }

    [HttpGet("/Protocolo/Novo")]
    public async Task<IActionResult> Novo(CancellationToken cancellationToken)
    {
        if (!Can("protocolo.criar")) return Forbid();
        return View(new ProtocoloFormViewModel());
    }

    [HttpPost("/Protocolo/Novo"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoPost(ProtocoloFormViewModel model, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.criar")) return Forbid();
        if (!ModelState.IsValid) return View("Novo", model);
        var id = await _service.CriarProtocoloAsync(model, cancellationToken);
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
        if (string.IsNullOrWhiteSpace(observacao)) { TempData["Warning"] = "A observação da tramitação é obrigatória."; return Redirect($"/Protocolo/Tramitar/{id}"); }
        var ok = await _service.TramitarProtocoloAsync(id, observacao, cancellationToken);
        await Audit("protocolo.tramitar", id.ToString(), cancellationToken);
        TempData[ok ? "Info" : "Warning"] = ok ? "Tramitação persistida em sigov.protocolo_movimento, tarefa, notificação e outbox." : "Tramitação não persistida porque o schema real está indisponível.";
        return Redirect($"/Protocolo/Detalhes/{id}");
    }

    [HttpPost("/Protocolo/Concluir/{id:long}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Concluir(long id, string? justificativa, CancellationToken cancellationToken) => AlterarStatus(id, "CONCLUIDO", justificativa, "protocolo.concluir", cancellationToken);

    [HttpPost("/Protocolo/Arquivar/{id:long}"), ValidateAntiForgeryToken]
    public Task<IActionResult> Arquivar(long id, string? justificativa, CancellationToken cancellationToken) => AlterarStatus(id, "ARQUIVADO", justificativa, "protocolo.arquivar", cancellationToken);

    private async Task<IActionResult> AlterarStatus(long id, string status, string? justificativa, string permission, CancellationToken ct)
    {
        if (!Can(permission)) return Forbid();
        if (string.IsNullOrWhiteSpace(justificativa)) { TempData["Warning"] = "Informe uma justificativa para concluir ou arquivar."; return Redirect($"/Protocolo/Detalhes/{id}"); }
        var ok = await _service.AlterarStatusProtocoloAsync(id, status, justificativa, ct);
        if (ok) await Audit(permission, id.ToString(), ct);
        TempData[ok ? "Info" : "Warning"] = ok ? $"Protocolo {status.ToLowerInvariant()} e evento registrado na timeline." : "Não foi possível alterar o protocolo neste tenant.";
        return Redirect($"/Protocolo/Detalhes/{id}");
    }

    [HttpGet("/Protocolo/{id:long}/CriarTarefa")]
    public IActionResult CriarTarefa(long id)
    {
        if (!Can("tarefa.criar")) return Forbid();
        ViewData["ProtocoloId"] = id;
        return View(new ProtocoloTarefaFormViewModel());
    }

    [HttpPost("/Protocolo/{id:long}/CriarTarefa"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarTarefaPost(long id, ProtocoloTarefaFormViewModel model, CancellationToken cancellationToken)
    {
        if (!Can("tarefa.criar")) return Forbid();
        if (!ModelState.IsValid) { ViewData["ProtocoloId"] = id; return View("CriarTarefa", model); }
        var tarefaId = await _service.CriarTarefaDoProtocoloAsync(id, model, HttpContext.TraceIdentifier, cancellationToken);
        if (tarefaId is null) { ModelState.AddModelError(string.Empty, "Não foi possível criar a tarefa. Verifique o protocolo e tente novamente."); ViewData["ProtocoloId"] = id; return View("CriarTarefa", model); }
        await Audit("protocolo.tarefa.criar", id.ToString(), cancellationToken);
        TempData["Info"] = "Tarefa criada, vinculada ao protocolo e notificação enviada ao responsável.";
        return Redirect($"/Tarefas/Detalhes/{tarefaId}");
    }

    [HttpGet("/Protocolo/{id:long}/VincularDocumento")]
    public IActionResult VincularDocumento(long id)
    {
        if (!Can("protocolo.anexar")) return Forbid();
        ViewData["ProtocoloId"] = id;
        return View(new ProtocoloDocumentoFormViewModel());
    }

    [HttpPost("/Protocolo/{id:long}/VincularDocumento"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VincularDocumentoPost(long id, ProtocoloDocumentoFormViewModel model, CancellationToken cancellationToken)
    {
        if (!Can("protocolo.anexar")) return Forbid();
        if (!ModelState.IsValid) { ViewData["ProtocoloId"] = id; return View("VincularDocumento", model); }
        if (!await _service.VincularDocumentoAsync(id, model.DocumentoId, HttpContext.TraceIdentifier, cancellationToken))
        { ModelState.AddModelError(string.Empty, "Protocolo ou documento não encontrado neste tenant."); ViewData["ProtocoloId"] = id; return View("VincularDocumento", model); }
        await Audit("protocolo.documento.vincular", id.ToString(), cancellationToken);
        TempData["Info"] = "Documento GED vinculado ao protocolo.";
        return Redirect($"/Protocolo/Detalhes/{id}");
    }

    [HttpPost("/Protocolo/{id:long}/Anexar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Anexar(long id, CancellationToken cancellationToken) { if (!Can("protocolo.anexar")) return Forbid(); await Audit("protocolo.anexar", id.ToString(), cancellationToken); return Redirect($"/Ged/NovoDocumento?protocolo_id={id}"); }
    private bool Can(string permission) => User.Identity?.IsAuthenticated == true && _permissions.HasPermission(User, permission);
    private Task Audit(string acao, string? id, CancellationToken ct) => _auditTrail.RegistrarAsync(null, null, acao, "protocolo", id, null, new { acao, id }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct);
}
