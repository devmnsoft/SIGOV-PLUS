using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.OrdemServico;

namespace Sigov.Api.Controllers.OrdemServico;

[ApiController, Authorize, Route("api/ordens-servico")]
public sealed class OrdensServicoController(IOrdemServicoApplicationService app) : ControllerBase
{
    private OrdemServicoContext Contexto(){if(!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value??User.FindFirst("tenant_id")?.Value,out var t)||t==Guid.Empty)throw new UnauthorizedAccessException("Tenant não resolvido.");if(!Guid.TryParse(User.FindFirst("sub")?.Value,out var u)||u==Guid.Empty)throw new UnauthorizedAccessException("Usuário não resolvido.");return new(t,u,HttpContext.TraceIdentifier);}
    private string Key()=>Request.Headers["Idempotency-Key"].ToString();
    [HttpGet,Authorize(Policy="os.ordens.visualizar")] public async Task<IActionResult> List([FromQuery]OrdemServicoFiltro f,CancellationToken ct)=>Ok(await app.ListarAsync(Contexto(),f,ct));
    [HttpGet("{id:guid}"),Authorize(Policy="os.ordens.visualizar")] public async Task<IActionResult> Get(Guid id,CancellationToken ct){var x=await app.ObterAsync(Contexto(),id,ct);return x is null?NotFound():Ok(x);}
    [HttpPost,Authorize(Policy="os.ordens.criar")] public async Task<IActionResult> Create(CriarOrdemServicoRequest r,CancellationToken ct){var id=await app.CriarAsync(Contexto(),r,Key(),ct);return CreatedAtAction(nameof(Get),new{id},id);}
    [HttpPost("{id:guid}/agendar"),Authorize(Policy="os.ordens.agendar")] public async Task<IActionResult> Schedule(Guid id,AgendarOrdemServicoRequest r,CancellationToken ct){await app.AgendarAsync(Contexto(),id,r,ct);return NoContent();}
    [HttpPost("{id:guid}/atribuir"),Authorize(Policy="os.ordens.atribuir")] public async Task<IActionResult> Assign(Guid id,AtribuirTecnicoRequest r,CancellationToken ct){await app.AtribuirAsync(Contexto(),id,r,ct);return NoContent();}
    [HttpPost("{id:guid}/iniciar-deslocamento"),Authorize(Policy="os.ordens.iniciar")] public Task<IActionResult> Travel(Guid id,IniciarOrdemServicoRequest r,CancellationToken ct)=>Change(id,"EM_DESLOCAMENTO",r.Version,null,r.InicioReal,ct);
    [HttpPost("{id:guid}/iniciar"),Authorize(Policy="os.ordens.iniciar")] public Task<IActionResult> Start(Guid id,IniciarOrdemServicoRequest r,CancellationToken ct)=>Change(id,"EM_EXECUCAO",r.Version,null,r.InicioReal,ct);
    [HttpPost("{id:guid}/pausar"),Authorize(Policy="os.ordens.pausar")] public Task<IActionResult> Pause(Guid id,PausarOrdemServicoRequest r,CancellationToken ct)=>Change(id,"PAUSADA",r.Version,r.Motivo,null,ct);
    [HttpPost("{id:guid}/retomar"),Authorize(Policy="os.ordens.pausar")] public Task<IActionResult> Resume(Guid id,RetomarOrdemServicoRequest r,CancellationToken ct)=>Change(id,"EM_EXECUCAO",r.Version,null,DateTimeOffset.UtcNow,ct);
    [HttpPost("{id:guid}/concluir"),Authorize(Policy="os.ordens.concluir")] public Task<IActionResult> Complete(Guid id,ConcluirOrdemServicoRequest r,CancellationToken ct)=>Change(id,"CONCLUIDA",r.Version,r.JustificativaItemNaoExecutado,null,ct);
    [HttpPost("{id:guid}/cancelar"),Authorize(Policy="os.ordens.cancelar")] public Task<IActionResult> Cancel(Guid id,CancelarOrdemServicoRequest r,CancellationToken ct)=>Change(id,"CANCELADA",r.Version,r.Motivo,null,ct);
    [HttpGet("agenda"),Authorize(Policy="os.ordens.visualizar")] public async Task<IActionResult> Agenda(DateTimeOffset inicio,DateTimeOffset fim,Guid? tecnicoId,CancellationToken ct)=>Ok(await app.AgendaAsync(Contexto(),inicio,fim,tecnicoId,ct));
    [HttpGet("dashboard"),Authorize(Policy="os.dashboard.visualizar")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(await app.DashboardAsync(Contexto(),ct));
    [HttpGet("{id:guid}/checklist"),Authorize(Policy="os.checklist.visualizar")] public async Task<IActionResult> Checklist(Guid id,CancellationToken ct)=>Ok(await app.ChecklistAsync(Contexto(),id,ct));
    [HttpPost("{id:guid}/checklist/respostas"),Authorize(Policy="os.checklist.responder")] public async Task<IActionResult> Answer(Guid id,ResponderChecklistRequest r,CancellationToken ct){await app.ResponderChecklistAsync(Contexto(),id,r,ct);return NoContent();}
    [HttpGet("{id:guid}/apontamentos"),Authorize(Policy="os.apontamentos.visualizar")] public async Task<IActionResult> Times(Guid id,CancellationToken ct)=>Ok(await app.ApontamentosAsync(Contexto(),id,ct));
    [HttpPost("{id:guid}/apontamentos"),Authorize(Policy="os.apontamentos.criar")] public async Task<IActionResult> Time(Guid id,RegistrarApontamentoRequest r,CancellationToken ct){await app.RegistrarApontamentoAsync(Contexto(),id,r,Key(),ct);return NoContent();}
    [HttpGet("{id:guid}/pecas"),Authorize(Policy="os.pecas.visualizar")] public async Task<IActionResult> Parts(Guid id,CancellationToken ct)=>Ok(await app.PecasAsync(Contexto(),id,ct));
    [HttpPost("{id:guid}/pecas/consumir"),Authorize(Policy="os.pecas.consumir")] public async Task<IActionResult> Consume(Guid id,ConsumirPecaRequest r,CancellationToken ct){await app.ConsumirPecaAsync(Contexto(),id,r,Key(),ct);return NoContent();}
    [HttpPost("{id:guid}/pecas/devolver"),Authorize(Policy="os.pecas.devolver")] public async Task<IActionResult> Return(Guid id,DevolverPecaRequest r,CancellationToken ct){await app.DevolverPecaAsync(Contexto(),id,r,Key(),ct);return NoContent();}
    private async Task<IActionResult> Change(Guid id,string status,long version,string? reason,DateTimeOffset? start,CancellationToken ct){await app.TransicionarAsync(Contexto(),id,status,version,reason,start,ct);return NoContent();}
}
