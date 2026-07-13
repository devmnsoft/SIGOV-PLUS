using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

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

public sealed class KanbanController : Controller
{
    private readonly IAuditTrailService _audit;
    public KanbanController(IAuditTrailService audit) { _audit = audit; }
    [Authorize]
    [HttpGet("/Kanban")]
    [HttpGet("/Kanban/Tarefas")]
    [HttpGet("/Kanban/OS")]
    [HttpGet("/Kanban/Propostas")]
    public IActionResult Index() => View("~/Views/Kanban/Index.cshtml");
    [HttpPost("/Kanban/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        await _audit.RegistrarAsync(null, null, "KANBAN_STATUS_ALTERAR", "Kanban", null, null, new { origem = "Web", fallback = "honesto" }, null, Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct);
        TempData["Toast"] = "Alteração registrada para auditoria; persistência real depende do schema/status da entidade.";
        return RedirectToAction(nameof(Index));
    }
}
