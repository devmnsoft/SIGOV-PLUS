using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Workflows;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workflows")]
public sealed class WorkflowsOperacionaisController : ControllerBase
{
    private readonly IWorkflowOperacionalService _service;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    public WorkflowsOperacionaisController(IWorkflowOperacionalService service, ICurrentTenant tenant, ICurrentUser user)
        => (_service, _tenant, _user) = (service, tenant, user);

    [HttpGet("tarefas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowTarefaDto>>>> Tarefas([FromQuery] string? modulo, [FromQuery] string? status, CancellationToken ct)
        => await Listar(null, modulo, status, ct).ConfigureAwait(false);

    [HttpGet("tarefas/minhas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowTarefaDto>>>> MinhasTarefas([FromQuery] string? modulo, [FromQuery] string? status, CancellationToken ct)
        => await Listar(_user.UsuarioId ?? -1, modulo, status, ct).ConfigureAwait(false);

    [HttpGet("{id:long}/historico")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowHistoricoDto>>>> Historico(long id, CancellationToken ct)
    {
        if (!TryTenant(out var tenantId)) return BadRequest(ApiResponse<IReadOnlyList<WorkflowHistoricoDto>>.Fail("Tenant obrigatório."));
        return Ok(ApiResponse<IReadOnlyList<WorkflowHistoricoDto>>.Ok(await _service.ListarHistoricoAsync(tenantId, id, ct).ConfigureAwait(false)));
    }

    [HttpPost("{id:long}/aprovar")] public Task<ActionResult<ApiResponse<object>>> Aprovar(long id, WorkflowDecisaoRequest request, CancellationToken ct) => Decidir(id,"APROVAR",request,ct);
    [HttpPost("{id:long}/reprovar")] public Task<ActionResult<ApiResponse<object>>> Reprovar(long id, WorkflowDecisaoRequest request, CancellationToken ct) => Decidir(id,"REPROVAR",request,ct);
    [HttpPost("{id:long}/encaminhar")] public Task<ActionResult<ApiResponse<object>>> Encaminhar(long id, WorkflowDecisaoRequest request, CancellationToken ct) => Decidir(id,"ENCAMINHAR",request,ct);
    [HttpPost("{id:long}/cancelar")] public Task<ActionResult<ApiResponse<object>>> Cancelar(long id, WorkflowDecisaoRequest request, CancellationToken ct) => Decidir(id,"CANCELAR",request,ct);

    private async Task<ActionResult<ApiResponse<IReadOnlyList<WorkflowTarefaDto>>>> Listar(long? responsavel, string? modulo, string? status, CancellationToken ct)
    {
        if (!TryTenant(out var tenantId)) return BadRequest(ApiResponse<IReadOnlyList<WorkflowTarefaDto>>.Fail("Tenant obrigatório."));
        var rows=await _service.ListarTarefasAsync(tenantId,responsavel,modulo,status,ct).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<WorkflowTarefaDto>>.Ok(rows));
    }

    private async Task<ActionResult<ApiResponse<object>>> Decidir(long id, string decisao, WorkflowDecisaoRequest request, CancellationToken ct)
    {
        if (!TryTenant(out var tenantId)) return BadRequest(ApiResponse<object>.Fail("Tenant obrigatório."));
        try
        {
            var changed=await _service.DecidirAsync(tenantId,id,_user.UsuarioId,decisao,request,HttpContext.TraceIdentifier,ct).ConfigureAwait(false);
            return changed ? Ok(ApiResponse<object>.Ok(new {id,decisao,correlationId=HttpContext.TraceIdentifier})) : NotFound(ApiResponse<object>.Fail("Workflow não encontrado ou já concluído."));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
    }

    private bool TryTenant(out long tenantId) { tenantId=_tenant.TenantId??0; return tenantId>0; }
}
