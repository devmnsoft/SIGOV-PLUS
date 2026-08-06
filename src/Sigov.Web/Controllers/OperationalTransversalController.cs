using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TarefasController : Controller
{
    private readonly TarefaService _service;
    private readonly IAuditTrailService _auditTrail;

    public TarefasController(TarefaService service, IAuditTrailService auditTrail)
    {
        _service = service;
        _auditTrail = auditTrail;
    }

    [HttpGet("/Tarefas")]
    [HttpGet("/Tarefas/Minhas")]
    [HttpGet("/Tarefas/Abertas")]
    [HttpGet("/Tarefas/Vencidas")]
    [HttpGet("/Tarefas/Equipe")]
    [HttpGet("/Tarefas/Detalhes/{id:long}")]
    [HttpGet("/Tarefas/{id:long}")]
    [HttpGet("/Tarefas/Nova")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("/Tarefas/Kanban")]
    public IActionResult Kanban() => Redirect("/Kanban/Tarefas");

    [HttpPost("/Tarefas/Nova")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Nova(CancellationToken cancellationToken) => PostAsync("TAREFA_CRIAR", cancellationToken);

    [HttpPost("/Tarefas/Sugerir")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Sugerir(CancellationToken cancellationToken) => PostAsync("TAREFA_SUGERIR_REGRA_SISTEMA", cancellationToken);

    [HttpPost("/Tarefas/{id:long}/Concluir")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Concluir(CancellationToken cancellationToken) => PostAsync("TAREFA_CONCLUIR", cancellationToken);

    [HttpPost("/Tarefas/{id:long}/Reabrir")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reabrir(CancellationToken cancellationToken) => PostAsync("TAREFA_REABRIR", cancellationToken);

    [HttpPost("/Tarefas/{id:long}/Delegar")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delegar(CancellationToken cancellationToken) => PostAsync("TAREFA_DELEGAR", cancellationToken);

    private async Task<IActionResult> PostAsync(string acao, CancellationToken cancellationToken)
    {
        await _auditTrail.RegistrarAsync(null, null, acao, "Tarefa", null, null, new { acao }, null, Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        TempData["Toast"] = "Operação de tarefa registrada; persistência real depende do schema sigov.tarefa.";
        return RedirectToAction(nameof(Index));
    }
}

[Authorize]
public sealed class AgendaController : Controller
{
    private readonly AgendaOperacionalService _service;
    private readonly IAuditTrailService _auditTrail;

    public AgendaController(AgendaOperacionalService service, IAuditTrailService auditTrail)
    {
        _service = service;
        _auditTrail = auditTrail;
    }

    [HttpGet("/Agenda")]
    [HttpGet("/Agenda/Prazos")]
    [HttpGet("/Agenda/Vencimentos")]
    [HttpGet("/Agenda/Calendario")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("/Agenda/SugerirPrazo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SugerirPrazo(CancellationToken cancellationToken)
    {
        await _auditTrail.RegistrarAsync(null, null, "AGENDA_SUGERIR_PRAZO", "Agenda", null, null, new { origem = "Regra do sistema" }, null, Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        TempData["Toast"] = "Prazo sugerido como regra do sistema; confirme antes de aplicar.";
        return RedirectToAction(nameof(Index));
    }
}

[Authorize]
public sealed class BiController : Controller
{
    private readonly BiOperacionalService _service;

    public BiController(BiOperacionalService service) => _service = service;

    [HttpGet("/Bi")]
    [HttpGet("/Bi/Governo")]
    [HttpGet("/Bi/Operacao")]
    [HttpGet("/Bi/Financeiro")]
    [HttpGet("/Bi/Documentos")]
    [HttpGet("/Bi/Saas")]
    [HttpGet("/Bi/Fluxos")]
    [HttpGet("/Bi/Protocolos")]
    [HttpGet("/Bi/Contratos")]
    [HttpGet("/Bi/Obras")]
    [HttpGet("/Bi/Suporte")]
    [HttpGet("/Bi/Portal")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(cancellationToken).ConfigureAwait(false));
}

[Authorize]
public sealed class MobileCampoController : Controller
{
    private readonly MobileCampoService _service;

    public MobileCampoController(MobileCampoService service) => _service = service;

    [HttpGet("/MobileCampo")]
    [HttpGet("/MobileCampo/Dashboard")]
    [HttpGet("/MobileCampo/Roteiros")]
    [HttpGet("/MobileCampo/Coletas")]
    [HttpGet("/MobileCampo/Sincronizacao")]
    [HttpGet("/MobileCampo/Dispositivos")]
    [HttpGet("/MobileCampo/Evidencias")]
    [HttpGet("/MobileCampo/Conflitos")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(cancellationToken).ConfigureAwait(false));
}

[Authorize]
public sealed class KanbanController : Controller
{
    private static readonly Guid DevelopmentTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IAuditTrailService _auditTrail;
    private readonly IEnterpriseCrudService _crud;
    private readonly IWebHostEnvironment _environment;

    public KanbanController(IAuditTrailService auditTrail, IEnterpriseCrudService crud, IWebHostEnvironment environment)
    {
        _auditTrail = auditTrail;
        _crud = crud;
        _environment = environment;
    }

    [HttpGet("/Kanban")]
    [HttpGet("/Kanban/Tarefas")]
    [HttpGet("/Kanban/OS")]
    [HttpGet("/Kanban/Propostas")]
    public async Task<IActionResult> Index([FromQuery] string? responsavel, [FromQuery] string? sla, CancellationToken cancellationToken)
    {
        var tipo = ResolveTipo(Request.Path.Value);
        var model = await LoadBoardAsync(tipo, ResolveTenantId(), responsavel, sla, cancellationToken).ConfigureAwait(false);
        return View("~/Views/Kanban/Index.cshtml", model);
    }

    [HttpPost("/Kanban/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status([FromForm] string tipo, [FromForm] Guid id, [FromForm] string status, CancellationToken cancellationToken)
    {
        var normalized = ResolveTipo(tipo);
        if (!AllowedStatuses(normalized).Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            TempData["Toast"] = "Status inválido para o quadro informado.";
            return RedirectToBoard(normalized);
        }

        var tenantId = ResolveTenantId();
        if (tenantId == Guid.Empty)
        {
            TempData["Toast"] = "Tenant obrigatório para alterar o Kanban.";
            return RedirectToBoard(normalized);
        }

        var correlationId = HttpContext.TraceIdentifier;
        var persisted = false;
        string message;
        if (normalized is "OS" or "Propostas")
        {
            var area = normalized == "OS" ? "os/ordens" : "comercial/propostas";
            EnterpriseExecutionContextAccessor.Current = new EnterpriseExecutionContext(tenantId, User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "web", User.Identity?.Name ?? "web", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), correlationId, User.Claims.Select(c => c.Value).ToArray());
            try
            {
                var result = await _crud.ExecuteActionAsync(area, id, status, tenantId, correlationId, cancellationToken).ConfigureAwait(false);
                persisted = result.Status != "SCHEMA_UNAVAILABLE" && result.Status != "NOT_FOUND";
                message = result.Message;
            }
            finally
            {
                EnterpriseExecutionContextAccessor.Current = null;
            }
        }
        else
        {
            message = "Tarefas usam schema sigov.tarefa; mudança foi auditada e não simula sucesso quando a tabela não estiver disponível.";
        }

        await _auditTrail.RegistrarAsync(null, null, "KANBAN_STATUS_ALTERAR", "Kanban", id.ToString(), null, new { tipo = normalized, status, persistido = persisted, evento = "kanban.status.alterado" }, null, Request.Headers.UserAgent.ToString(), correlationId, cancellationToken).ConfigureAwait(false);
        TempData["Toast"] = persisted ? "Status atualizado e auditado." : message;
        return RedirectToBoard(normalized);
    }

    private async Task<KanbanBoardViewModel> LoadBoardAsync(string tipo, Guid tenantId, string? responsavel, string? sla, CancellationToken cancellationToken)
    {
        var statuses = AllowedStatuses(tipo);
        var cards = new List<KanbanCardViewModel>();
        var source = tenantId == Guid.Empty ? "tenant_obrigatorio" : "fallback_honesto";
        if (tenantId != Guid.Empty && tipo is "OS" or "Propostas")
        {
            var area = tipo == "OS" ? "os/ordens" : "comercial/propostas";
            var rows = await _crud.ListAsync(area, tenantId, 1, 200, null, cancellationToken).ConfigureAwait(false);
            cards.AddRange(rows.Select(row => new KanbanCardViewModel(row.Id, row.Name, row.Status, $"/Enterprise/{area}/{row.Id}", "real", row.UpdatedAt)));
            if (cards.Count > 0) source = "real";
        }

        return new KanbanBoardViewModel(tipo, statuses, cards, source, responsavel, sla);
    }

    private IActionResult RedirectToBoard(string tipo) => Redirect(tipo switch { "OS" => "/Kanban/OS", "Propostas" => "/Kanban/Propostas", _ => "/Kanban/Tarefas" });

    private Guid ResolveTenantId()
    {
        if (Guid.TryParse(User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenant")?.Value, out var tenant)) return tenant;
        return _environment.IsProduction() ? Guid.Empty : DevelopmentTenantId;
    }

    private static string ResolveTipo(string? value) => (value ?? string.Empty).Contains("OS", StringComparison.OrdinalIgnoreCase) ? "OS" : (value ?? string.Empty).Contains("Propostas", StringComparison.OrdinalIgnoreCase) ? "Propostas" : "Tarefas";

    private static string[] AllowedStatuses(string tipo) => tipo switch
    {
        "OS" => new[] { "ABERTA", "AGENDADA", "EM_EXECUCAO", "PAUSADA", "CONCLUIDA", "CANCELADA" },
        "Propostas" => new[] { "RASCUNHO", "ENVIADA", "EM_ANALISE", "APROVADA", "REPROVADA", "CONVERTIDA_EM_PEDIDO" },
        _ => new[] { "ABERTA", "EM_ANDAMENTO", "AGUARDANDO", "CONCLUIDA", "VENCIDA" }
    };
}

public sealed record KanbanBoardViewModel(string Tipo, IReadOnlyList<string> Colunas, IReadOnlyList<KanbanCardViewModel> Cards, string Fonte, string? Responsavel, string? Sla);

public sealed record KanbanCardViewModel(Guid Id, string Titulo, string Status, string Link, string Fonte, DateTimeOffset AtualizadoEm);
