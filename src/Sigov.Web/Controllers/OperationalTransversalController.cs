using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;
using Sigov.Application.Enterprise;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TarefasController : Controller
{
    private readonly TarefaService _s; private readonly IAuditTrailService _a; public TarefasController(TarefaService s, IAuditTrailService a){_s=s;_a=a;}
    [HttpGet("/Tarefas")][HttpGet("/Tarefas/Minhas")][HttpGet("/Tarefas/Abertas")][HttpGet("/Tarefas/Vencidas")][HttpGet("/Tarefas/{id:long}")][HttpGet("/Tarefas/Nova")] public async Task<IActionResult> Index(CancellationToken ct)=>View("~/Views/Operational/Hub.cshtml", await _s.GetAsync(ct));
    [HttpPost("/Tarefas/Nova")][ValidateAntiForgeryToken] public Task<IActionResult> Nova(CancellationToken ct)=>Post("TAREFA_CRIAR",ct);
    [HttpPost("/Tarefas/Sugerir")][ValidateAntiForgeryToken] public Task<IActionResult> Sugerir(CancellationToken ct)=>Post("TAREFA_SUGERIR_REGRA_SISTEMA",ct);
    [HttpPost("/Tarefas/{id:long}/Concluir")][ValidateAntiForgeryToken] public Task<IActionResult> Concluir(CancellationToken ct)=>Post("TAREFA_CONCLUIR",ct);
    [HttpPost("/Tarefas/{id:long}/Reabrir")][ValidateAntiForgeryToken] public Task<IActionResult> Reabrir(CancellationToken ct)=>Post("TAREFA_REABRIR",ct);
    [HttpPost("/Tarefas/{id:long}/Delegar")][ValidateAntiForgeryToken] public Task<IActionResult> Delegar(CancellationToken ct)=>Post("TAREFA_DELEGAR",ct);
    private async Task<IActionResult> Post(string acao,CancellationToken ct){await _a.RegistrarAsync(null,null,acao,"Tarefa",null,null,new{acao},null,Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct);TempData["Toast"]="Operação de tarefa registrada; persistência real depende do schema sigov.tarefa.";return RedirectToAction(nameof(Index));}
}

public sealed class AgendaController : Controller { private readonly AgendaOperacionalService _s; private readonly IAuditTrailService _a; public AgendaController(AgendaOperacionalService s, IAuditTrailService a){_s=s;_a=a;} [Authorize][HttpGet("/Agenda")][HttpGet("/Agenda/Prazos")][HttpGet("/Agenda/Vencimentos")][HttpGet("/Agenda/Calendario")] public async Task<IActionResult> Index(CancellationToken ct)=>View("~/Views/Operational/Hub.cshtml", await _s.GetAsync(ct)); [HttpPost("/Agenda/SugerirPrazo")][ValidateAntiForgeryToken] public async Task<IActionResult> SugerirPrazo(CancellationToken ct){ await _a.RegistrarAsync(null,null,"AGENDA_SUGERIR_PRAZO","Agenda",null,null,new{origem="Regra do sistema"},null,Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct); TempData["Toast"]="Prazo sugerido como regra do sistema; confirme antes de aplicar."; return RedirectToAction(nameof(Index)); } }
public sealed class BiController : Controller { private readonly BiOperacionalService _s; public BiController(BiOperacionalService s){_s=s;} [Authorize][HttpGet("/Bi")][HttpGet("/Bi/Governo")][HttpGet("/Bi/Operacao")][HttpGet("/Bi/Financeiro")][HttpGet("/Bi/Documentos")][HttpGet("/Bi/Saas")][HttpGet("/Bi/Fluxos")][HttpGet("/Bi/Protocolos")][HttpGet("/Bi/Contratos")][HttpGet("/Bi/Obras")][HttpGet("/Bi/Suporte")][HttpGet("/Bi/Portal")] public async Task<IActionResult> Index(CancellationToken ct)=>View("~/Views/Operational/Hub.cshtml", await _s.GetAsync(ct)); }
public sealed class MobileCampoController : Controller { private readonly MobileCampoService _s; public MobileCampoController(MobileCampoService s){_s=s;} [Authorize][HttpGet("/MobileCampo")][HttpGet("/MobileCampo/Dashboard")][HttpGet("/MobileCampo/Roteiros")][HttpGet("/MobileCampo/Coletas")][HttpGet("/MobileCampo/Sincronizacao")][HttpGet("/MobileCampo/Dispositivos")][HttpGet("/MobileCampo/Evidencias")][HttpGet("/MobileCampo/Conflitos")] public async Task<IActionResult> Index(CancellationToken ct)=>View("~/Views/Operational/Hub.cshtml", await _s.GetAsync(ct)); }

[Authorize]
public sealed class KanbanController : Controller
{
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IAuditTrailService _audit;
    private readonly IEnterpriseCrudService? _crud;
    private readonly IWebHostEnvironment _environment;
    public KanbanController(IAuditTrailService audit, IEnterpriseModuleService enterprise, IWebHostEnvironment environment) { _audit = audit; _crud = enterprise as IEnterpriseCrudService; _environment = environment; }

    [HttpGet("/Kanban")]
    [HttpGet("/Kanban/Tarefas")]
    [HttpGet("/Kanban/OS")]
    [HttpGet("/Kanban/Propostas")]
    public async Task<IActionResult> Index([FromQuery] string? responsavel, [FromQuery] string? sla, CancellationToken ct)
    {
        var tipo = ResolveTipo(Request.Path.Value);
        var model = await LoadBoardAsync(tipo, ResolveTenantId(), responsavel, sla, ct).ConfigureAwait(false);
        return View("~/Views/Kanban/Index.cshtml", model);
    }

    [HttpPost("/Kanban/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status([FromForm] string tipo, [FromForm] Guid id, [FromForm] string status, CancellationToken ct)
    {
        var normalized = ResolveTipo(tipo);
        if (!AllowedStatuses(normalized).Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            TempData["Toast"] = "Status inválido para o quadro informado.";
            return RedirectToBoard(normalized);
        }

        var tenantId = ResolveTenantId();
        var correlationId = HttpContext.TraceIdentifier;
        var persisted = false;
        string message;
        if (_crud is not null && normalized is "OS" or "Propostas")
        {
            var area = normalized == "OS" ? "os/ordens" : "comercial/propostas";
            EnterpriseExecutionContextAccessor.Current = new EnterpriseExecutionContext(tenantId, User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "web", User.Identity?.Name ?? "web", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), correlationId, User.Claims.Select(c => c.Value).ToArray());
            try
            {
                var result = await _crud.ExecuteActionAsync(area, id, status, tenantId, correlationId, ct).ConfigureAwait(false);
                persisted = result.Status != "SCHEMA_UNAVAILABLE" && result.Status != "NOT_FOUND";
                message = result.Message;
            }
            finally { EnterpriseExecutionContextAccessor.Current = null; }
        }
        else
        {
            message = "Tarefas usam schema sigov.tarefa; mudança foi auditada e não simula sucesso quando a tabela não estiver disponível.";
        }

        await _audit.RegistrarAsync(null, null, "KANBAN_STATUS_ALTERAR", "Kanban", id.ToString(), null, new { tipo = normalized, status, persistido = persisted, evento = "kanban.status.alterado" }, null, Request.Headers.UserAgent.ToString(), correlationId, ct);
        TempData["Toast"] = persisted ? "Status atualizado e auditado." : message;
        return RedirectToBoard(normalized);
    }

    private async Task<KanbanBoardViewModel> LoadBoardAsync(string tipo, Guid tenantId, string? responsavel, string? sla, CancellationToken ct)
    {
        var statuses = AllowedStatuses(tipo);
        var cards = new List<KanbanCardViewModel>();
        var source = "fallback_honesto";
        if (_crud is not null && tipo is "OS" or "Propostas")
        {
            var area = tipo == "OS" ? "os/ordens" : "comercial/propostas";
            var rows = await _crud.ListAsync(area, tenantId, 1, 200, null, ct).ConfigureAwait(false);
            cards.AddRange(rows.Select(r => new KanbanCardViewModel(r.Id, r.Name, r.Status, $"/Enterprise/{area}/{r.Id}", "real", r.UpdatedAt)));
            if (cards.Count > 0) source = "real";
        }
        return new KanbanBoardViewModel(tipo, statuses, cards, source, responsavel, sla);
    }

    private IActionResult RedirectToBoard(string tipo) => Redirect(tipo switch { "OS" => "/Kanban/OS", "Propostas" => "/Kanban/Propostas", _ => "/Kanban/Tarefas" });
    private Guid ResolveTenantId()
    {
        if (Guid.TryParse(User.FindFirst("tenant_id")?.Value ?? User.FindFirst("tenant")?.Value, out var tenant)) return tenant;
        return _environment.IsProduction() ? Guid.Empty : DemoTenantId;
    }
    private static string ResolveTipo(string? value) => (value ?? string.Empty).Contains("OS", StringComparison.OrdinalIgnoreCase) ? "OS" : (value ?? string.Empty).Contains("Propostas", StringComparison.OrdinalIgnoreCase) ? "Propostas" : "Tarefas";
    private static string[] AllowedStatuses(string tipo) => tipo switch { "OS" => new[] { "ABERTA", "AGENDADA", "EM_EXECUCAO", "PAUSADA", "CONCLUIDA", "CANCELADA" }, "Propostas" => new[] { "RASCUNHO", "ENVIADA", "EM_ANALISE", "APROVADA", "REPROVADA", "CONVERTIDA_EM_PEDIDO" }, _ => new[] { "ABERTA", "EM_ANDAMENTO", "AGUARDANDO", "CONCLUIDA", "VENCIDA" } };
}

public sealed record KanbanBoardViewModel(string Tipo, IReadOnlyList<string> Colunas, IReadOnlyList<KanbanCardViewModel> Cards, string Fonte, string? Responsavel, string? Sla);
public sealed record KanbanCardViewModel(Guid Id, string Titulo, string Status, string Link, string Fonte, DateTimeOffset AtualizadoEm);
