using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Workflows;
using Sigov.Web.Services;
using Sigov.Web.Services.Workflows;

namespace Sigov.Web.Controllers;

[Authorize(Policy = "WORKFLOW_CONSULTA")]
[Route("Workflows")]
public sealed class WorkflowsController : Controller
{
    private readonly WorkflowRepository _repository;
    private readonly WorkflowValidationService _validation;
    private readonly ITenantContextAccessor _tenant;
    private readonly IAuditTrailService _audit;
    public WorkflowsController(WorkflowRepository repository, WorkflowValidationService validation, ITenantContextAccessor tenant, IAuditTrailService audit) => (_repository,_validation,_tenant,_audit)=(repository,validation,tenant,audit);

    [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct) => View(await _repository.ListAsync(TenantId(),ct));
    [HttpGet("Novo"), Authorize(Policy="WORKFLOW_GERENCIAR")] public IActionResult Novo() => View(new CreateWorkflowInput());
    [HttpPost("Novo"), ValidateAntiForgeryToken, Authorize(Policy="WORKFLOW_GERENCIAR")]
    public async Task<IActionResult> Novo(CreateWorkflowInput input,CancellationToken ct)
    { if(!ModelState.IsValid) return View(input); var id=await _repository.CreateAsync(TenantId(),UserId(),input,ct); await Audit("WORKFLOW_CRIADO",id,ct); return RedirectToAction(nameof(Designer),new{id}); }
    [HttpGet("Editar/{id:long}")] public Task<IActionResult> Editar(long id,CancellationToken ct)=>Designer(id,ct);
    [HttpGet("Detalhes/{id:long}")] public Task<IActionResult> Detalhes(long id,CancellationToken ct)=>Designer(id,ct);
    [HttpGet("Designer/{id:long}")] public async Task<IActionResult> Designer(long id,CancellationToken ct) { var model=await _repository.GetDesignerAsync(TenantId(),id,ct); return model is null?NotFound():View(model); }
    [HttpPost("Designer/{id:long}"),ValidateAntiForgeryToken,Authorize(Policy="WORKFLOW_GERENCIAR")]
    public async Task<IActionResult> SalvarDesigner(long id,[FromBody] SaveWorkflowDesignInput input,CancellationToken ct)
    { var errors=_validation.Validate(input); if(errors.Count>0) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string,string[]>{{"design",errors.ToArray()}})); await _repository.SaveDesignAsync(TenantId(),id,input,ct); await Audit("WORKFLOW_DESIGN_ATUALIZADO",id,ct); return Ok(new{message="Designer salvo com segurança."}); }
    [HttpPost("Publicar/{id:long}"),ValidateAntiForgeryToken,Authorize(Policy="WORKFLOW_GERENCIAR")]
    public async Task<IActionResult> Publicar(long id,CancellationToken ct) { var model=await _repository.GetDesignerAsync(TenantId(),id,ct); if(model is null)return NotFound(); var input=new SaveWorkflowDesignInput{Etapas=model.Etapas.Select(x=>new WorkflowStepInput{Id=x.Id,Nome=x.Nome,Inicial=x.Inicial,Final=x.Final}).ToList()}; var errors=_validation.Validate(input); if(errors.Count>0){TempData["Error"]=string.Join(" ",errors);return RedirectToAction(nameof(Designer),new{id});} await _repository.PublishAsync(TenantId(),id,UserId(),ct); await Audit("WORKFLOW_PUBLICADO",id,ct); TempData["Toast"]="Workflow publicado e bloqueado para edição."; return RedirectToAction(nameof(Designer),new{id}); }
    [HttpGet("Versoes/{id:long}")] public Task<IActionResult> Versoes(long id,CancellationToken ct)=>Designer(id,ct);
    private long TenantId()=>_tenant.Resolve().TenantId??throw new InvalidOperationException("Tenant obrigatório para operar workflows.");
    private long? UserId()=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
    private Task Audit(string action,long id,CancellationToken ct)=>_audit.RegistrarAsync(TenantId(),UserId(),action,"workflow_definicao",id.ToString(),null,new{id},HttpContext.Connection.RemoteIpAddress?.ToString(),Request.Headers.UserAgent.ToString(),HttpContext.TraceIdentifier,ct);
}
