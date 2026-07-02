using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Operational;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class WorkflowController : Controller
{
    private readonly WorkflowService _service; private readonly IAuditTrailService _audit; private readonly OperationalEventService _events; private readonly ILogger<WorkflowController> _logger;
    public WorkflowController(WorkflowService service, IAuditTrailService audit, OperationalEventService events, ILogger<WorkflowController> logger) { _service=service; _audit=audit; _events=events; _logger=logger; }
    [HttpGet("/Workflow")] public Task<IActionResult> Index(CancellationToken ct) => ViewHub(ct);
    [HttpGet("/Workflow/Definicoes")] public Task<IActionResult> Definicoes(CancellationToken ct) => ViewHub(ct);
    [HttpGet("/Workflow/Definicoes/Nova")] public Task<IActionResult> Nova(CancellationToken ct) => ViewHub(ct);
    [HttpPost("/Workflow/Definicoes/Nova")][ValidateAntiForgeryToken] public async Task<IActionResult> NovaPost(CancellationToken ct) { TempData["Toast"]="Workflow salvo apenas quando o schema sigov.workflow existir; fallback honesto ativo."; await AuditAsync("WORKFLOW_DEFINICAO_SOLICITADA", null, ct); return RedirectToAction(nameof(Definicoes)); }
    [HttpGet("/Workflow/Instancias")] public Task<IActionResult> Instancias(CancellationToken ct) => ViewHub(ct);
    [HttpGet("/Workflow/Instancias/{id:long}")] public Task<IActionResult> Instancia(long id, CancellationToken ct) => ViewHub(ct);
    [HttpPost("/Workflow/{id:long}/SugerirProximaEtapa")][ValidateAntiForgeryToken] public async Task<IActionResult> SugerirProximaEtapa(long id, CancellationToken ct) { await AuditAsync("WORKFLOW_SUGERIR_PROXIMA_ETAPA", id.ToString(), ct); TempData["Toast"]="Sugestão registrada como regra do sistema; nenhuma etapa foi executada automaticamente."; return RedirectToAction(nameof(Index)); }
    [HttpPost("/Workflow/Instancias/{id:long}/Avancar")][ValidateAntiForgeryToken] public Task<IActionResult> Avancar(long id, WorkflowAdvanceInput input, CancellationToken ct) => Critical(id,"WORKFLOW_AVANCAR",ct);
    [HttpPost("/Workflow/Instancias/{id:long}/Cancelar")][ValidateAntiForgeryToken] public Task<IActionResult> Cancelar(long id, CancellationToken ct) => Critical(id,"WORKFLOW_CANCELAR",ct);
    [HttpPost("/Workflow/Instancias/{id:long}/Arquivar")][ValidateAntiForgeryToken] public Task<IActionResult> Arquivar(long id, CancellationToken ct) => Critical(id,"WORKFLOW_ARQUIVAR",ct);
    private async Task<IActionResult> ViewHub(CancellationToken ct) { try { return View("~/Views/Operational/Hub.cshtml", await _service.GetAsync(ct)); } catch(Exception ex) { _logger.LogError(ex,"Falha Workflow"); return View("~/Views/Operational/Hub.cshtml", new OperationalHubViewModel{AreaKey="Workflow",Title="Workflow",Description="Falha controlada ao carregar workflow."}); } }
    private async Task<IActionResult> Critical(long id, string action, CancellationToken ct) { await AuditAsync(action,id.ToString(),ct); await _events.TryRegisterAsync(action,"Workflow","workflow_instancia",id.ToString(),new{id},ct); TempData["Toast"]="Ação registrada com auditoria; persistência depende do schema operacional."; return RedirectToAction(nameof(Instancias)); }
    private Task AuditAsync(string acao,string? id,CancellationToken ct)=>_audit.RegistrarAsync(null,null,acao,"Workflow",id,null,new{acao,id},HttpContext.Connection.RemoteIpAddress?.ToString(),Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct);
}
