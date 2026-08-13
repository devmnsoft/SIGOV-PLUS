using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.ExecutiveOperations;

namespace Sigov.Api.Controllers;

[ApiController]
public abstract class TenantExecutiveController(ICurrentTenant tenant, ICurrentUser user, IExecutiveOperationsRepository repository) : ControllerBase
{
    protected long TenantId => tenant.TenantId ?? 0;
    protected long UserId => user.UsuarioId ?? 0;
    protected IExecutiveOperationsRepository Repository => repository;
    protected ActionResult TenantRequired() => BadRequest(new { error = "Tenant obrigatório." });
}

[Route("api/governanca-operacional")]
public sealed class GovernancaOperacionalController(ICurrentTenant tenant, ICurrentUser user, IExecutiveOperationsRepository repository, IGovernancaOperacionalService service) : TenantExecutiveController(tenant,user,repository)
{
    [HttpGet("resumo")] public async Task<ActionResult> Resumo(CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await service.SummaryAsync(TenantId,UserId,ct));
    [HttpGet("pendencias")] public async Task<ActionResult> Pendencias([FromQuery] OperationFilter filter,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await service.PendenciesAsync(TenantId,filter,ct));
    [HttpGet("indicadores")] public async Task<ActionResult> Indicadores([FromQuery]string? module,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await service.IndicatorsAsync(TenantId,module,ct));
    [HttpGet("riscos")] public async Task<ActionResult> Riscos([FromQuery] OperationFilter filter,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await service.PendenciesAsync(TenantId,filter with{Severity="CRITICA"},ct));
    [HttpGet("agenda")] public async Task<ActionResult> Agenda([FromQuery] OperationFilter filter,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await service.PendenciesAsync(TenantId,filter,ct));
}

[Route("api/notificacoes")]
public sealed class InternalNotificationsController(ICurrentTenant tenant,ICurrentUser user,IExecutiveOperationsRepository repository):TenantExecutiveController(tenant,user,repository)
{
    [HttpGet] public async Task<ActionResult> List([FromQuery]OperationFilter filter,CancellationToken ct)=>Valid()?Ok(await Repository.NotificationsAsync(TenantId,UserId,filter,false,ct)):TenantRequired();
    [HttpGet("nao-lidas")] public async Task<ActionResult> Unread([FromQuery]OperationFilter filter,CancellationToken ct)=>Valid()?Ok(await Repository.NotificationsAsync(TenantId,UserId,filter,true,ct)):TenantRequired();
    [HttpPost("{id:long}/marcar-lida")] public async Task<ActionResult> Read(long id,CancellationToken ct){if(!Valid())return TenantRequired();await Repository.MarkNotificationAsync(TenantId,UserId,id,false,ct);return NoContent();}
    [HttpPost("marcar-todas-lidas")] public async Task<ActionResult> ReadAll(CancellationToken ct){if(!Valid())return TenantRequired();await Repository.MarkNotificationAsync(TenantId,UserId,null,false,ct);return NoContent();}
    [HttpPost("{id:long}/arquivar")] public async Task<ActionResult> Archive(long id,CancellationToken ct){if(!Valid())return TenantRequired();await Repository.MarkNotificationAsync(TenantId,UserId,id,true,ct);return NoContent();}
    [HttpGet("preferencias")] public async Task<ActionResult> Preferences(CancellationToken ct)=>Valid()?Content(await Repository.GetPreferencesAsync(TenantId,UserId,ct),"application/json"):TenantRequired();
    [HttpPut("preferencias")] public async Task<ActionResult> Preferences([FromBody]System.Text.Json.JsonElement body,CancellationToken ct){if(!Valid())return TenantRequired();await Repository.SetPreferencesAsync(TenantId,UserId,body.GetRawText(),ct);return NoContent();}
    private bool Valid()=>TenantId>0&&UserId>0;
}

[Route("api/integracoes-internas")]
public sealed class InternalIntegrationsController(ICurrentTenant tenant,ICurrentUser user,IExecutiveOperationsRepository repository):TenantExecutiveController(tenant,user,repository)
{
    [HttpGet] public async Task<ActionResult> List([FromQuery]OperationFilter filter,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await Repository.IntegrationsAsync(TenantId,filter,ct));
    [HttpPost("{id:long}/reprocessar")] public async Task<ActionResult> Retry(long id,CancellationToken ct){if(TenantId<=0)return TenantRequired();await Repository.ChangeIntegrationAsync(TenantId,id,"PENDENTE",UserId,HttpContext.TraceIdentifier,ct);return Accepted();}
    [HttpPost("{id:long}/cancelar")] public async Task<ActionResult> Cancel(long id,CancellationToken ct){if(TenantId<=0)return TenantRequired();await Repository.ChangeIntegrationAsync(TenantId,id,"CANCELADO",UserId,HttpContext.TraceIdentifier,ct);return NoContent();}
}

[Route("api/qualidade-dados")]
public sealed class DataQualityController(ICurrentTenant tenant,ICurrentUser user,IExecutiveOperationsRepository repository):TenantExecutiveController(tenant,user,repository)
{
    [HttpGet("resumo")] public async Task<ActionResult> Summary(CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await Repository.QualitySummaryAsync(TenantId,ct));
    [HttpGet("educacao")] public Task<ActionResult> Education([FromQuery]OperationFilter filter,CancellationToken ct)=>ByModule("EDUCACAO",filter,ct);
    [HttpGet("rh")] public Task<ActionResult> Rh([FromQuery]OperationFilter filter,CancellationToken ct)=>ByModule("RH",filter,ct);
    [HttpGet("folha")] public Task<ActionResult> Payroll([FromQuery]OperationFilter filter,CancellationToken ct)=>ByModule("FOLHA",filter,ct);
    [HttpPost("reprocessar")] public async Task<ActionResult> Reprocess(CancellationToken ct){if(TenantId<=0)return TenantRequired();await Repository.ReprocessQualityAsync(TenantId,UserId,HttpContext.TraceIdentifier,ct);return Accepted();}
    private async Task<ActionResult> ByModule(string module,OperationFilter filter,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(await Repository.QualityAsync(TenantId,filter with{Module=module},ct));
}

[Route("api/assistentes-operacionais")]
public sealed class OperationalAssistantsController(ICurrentTenant tenant,ICurrentUser user,IExecutiveOperationsRepository repository):TenantExecutiveController(tenant,user,repository)
{
    [HttpPost("salvar-etapa")] public async Task<ActionResult> Save(AssistantCommand command,CancellationToken ct)
    {
        if(TenantId<=0||UserId<=0)return TenantRequired();
        if(string.IsNullOrWhiteSpace(command.Assistant)||string.IsNullOrWhiteSpace(command.Step))return BadRequest(new{error="Assistente e etapa são obrigatórios."});
        try{using var _=System.Text.Json.JsonDocument.Parse(command.Payload);}catch(System.Text.Json.JsonException){return BadRequest(new{error="Payload JSON inválido."});}
        return Ok(await Repository.SaveAssistantAsync(TenantId,UserId,command,HttpContext.TraceIdentifier,ct));
    }
}

[Route("api")]
public sealed class ModuleExecutiveManagementController(ICurrentTenant tenant,ICurrentUser user,IExecutiveOperationsRepository repository,IGovernancaOperacionalService service):TenantExecutiveController(tenant,user,repository)
{
    [HttpGet("educacao/gestao-executiva")] public Task<ActionResult> Education(CancellationToken ct)=>Module("EDUCACAO",ct);
    [HttpGet("rh/gestao-executiva")] public Task<ActionResult> HumanResources(CancellationToken ct)=>Module("RH",ct);
    [HttpGet("rh/folha/gestao-executiva")] public Task<ActionResult> Payroll(CancellationToken ct)=>Module("FOLHA",ct);
    private async Task<ActionResult> Module(string module,CancellationToken ct)=>TenantId<=0?TenantRequired():Ok(new{module,indicators=await service.IndicatorsAsync(TenantId,module,ct),quality=await Repository.QualityAsync(TenantId,new OperationFilter(Module:module,PageSize:10),ct)});
}
