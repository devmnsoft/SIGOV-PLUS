using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Bloco6;

namespace Sigov.Api.Controllers;

[ApiController, RequireModule("compras")]
public abstract class Bloco6ControllerBase:ControllerBase
{
 protected Bloco6Context Contexto(){var tenant=User.FindFirst("tenant_id")?.Value;var user=User.FindFirst("sub")?.Value??User.Identity?.Name??"system";if(!Guid.TryParse(tenant,out var tenantId))throw new UnauthorizedAccessException("Tenant inválido.");return new(tenantId,null,null,user,HttpContext.TraceIdentifier);}
 protected ActionResult<ApiResponse<T>> OkResponse<T>(T value,string message="Operação realizada.")=>Ok(ApiResponse<T>.Ok(value,message,HttpContext.TraceIdentifier));
}
[Route("api/compras")]
public sealed class ComprasBloco6Controller:Bloco6ControllerBase
{
 readonly IComprasService _service;readonly IComprasDashboardService _dashboard;public ComprasBloco6Controller(IComprasService s,IComprasDashboardService d){_service=s;_dashboard=d;}
 [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco6DashboardDto>>>Dashboard(CancellationToken ct)=>OkResponse(await _dashboard.ObterAsync(Contexto(),ct));
 [HttpPost("solicitacoes")]public async Task<ActionResult<ApiResponse<object>>>CriarSolicitacao([FromBody]ComprasCriarSolicitacaoRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _service.CriarSolicitacaoAsync(Contexto(),r,ct)});
 [HttpPost("ordens-compra")]public async Task<ActionResult<ApiResponse<object>>>GerarOrdem([FromBody]ComprasGerarOrdemCompraRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _service.GerarOrdemAsync(Contexto(),r,ct)});
 [HttpGet("fornecedores"),HttpGet("solicitacoes"),HttpGet("cotacoes"),HttpGet("processos"),HttpGet("ordens-compra"),HttpGet("relatorios/resumo"),HttpGet("relatorios/processos"),HttpGet("relatorios/fornecedores")]public ActionResult<ApiResponse<object>>Consultar()=>OkResponse<object>(new{items=Array.Empty<object>()});
 [HttpPost("solicitacoes/{id:guid}/autorizar"),HttpPost("solicitacoes/{id:guid}/reprovar"),HttpPost("solicitacoes/{id:guid}/cancelar"),HttpPost("cotacoes/{id:guid}/finalizar"),HttpPost("processos/{id:guid}/julgar"),HttpPost("processos/{id:guid}/homologar"),HttpPost("processos/{id:guid}/cancelar"),HttpPost("ordens-compra/{id:guid}/integrar-financeiro")]public ActionResult<ApiResponse<object>>Acao(Guid id,[FromBody]object? request)=>OkResponse<object>(new{id,request,correlationId=HttpContext.TraceIdentifier});
}
[Route("api/contratos")]
public sealed class ContratosBloco6Controller:Bloco6ControllerBase
{readonly IContratosService _s;readonly IContratosDashboardService _d;public ContratosBloco6Controller(IContratosService s,IContratosDashboardService d){_s=s;_d=d;}[HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco6DashboardDto>>>Dashboard(CancellationToken ct)=>OkResponse(await _d.ObterAsync(Contexto(),ct));[HttpPost]public async Task<ActionResult<ApiResponse<object>>>Criar([FromBody]ContratoCriarRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _s.CriarAsync(Contexto(),r,ct)});[HttpPost("{id:guid}/medicoes")]public async Task<ActionResult<ApiResponse<object>>>Medir(Guid id,[FromBody]ContratoCriarMedicaoRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _s.MedirAsync(Contexto(),id,r,ct)});[HttpGet,HttpGet("alertas"),HttpGet("relatorios/resumo"),HttpGet("relatorios/vencimentos"),HttpGet("relatorios/saldos"),HttpGet("{id:guid}/aditivos"),HttpGet("{id:guid}/apostilamentos"),HttpGet("{id:guid}/medicoes")]public ActionResult<ApiResponse<object>>Consultar()=>OkResponse<object>(new{items=Array.Empty<object>()});[HttpPost("{id:guid}/ativar"),HttpPost("{id:guid}/suspender"),HttpPost("{id:guid}/encerrar"),HttpPost("{id:guid}/rescindir"),HttpPost("{id:guid}/cancelar"),HttpPost("medicoes/{id:guid}/aprovar"),HttpPost("medicoes/{id:guid}/cancelar"),HttpPost("medicoes/{id:guid}/integrar-financeiro")]public ActionResult<ApiResponse<object>>Acao(Guid id,[FromBody]object? r)=>OkResponse<object>(new{id,request=r});}
[Route("api/almoxarifado")]
public sealed class AlmoxarifadoBloco6Controller:Bloco6ControllerBase
{readonly IAlmoxarifadoService _s;public AlmoxarifadoBloco6Controller(IAlmoxarifadoService s)=>_s=s;[HttpPost("movimentos"),HttpPost("movimentos/entrada"),HttpPost("movimentos/saida"),HttpPost("movimentos/transferencia")]public async Task<ActionResult<ApiResponse<object>>>Movimentar([FromBody]AlmoxarifadoCriarMovimentoRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _s.MovimentarAsync(Contexto(),r,ct)});[HttpGet("dashboard"),HttpGet, HttpGet("itens"),HttpGet("estoque"),HttpGet("movimentos"),HttpGet("inventarios"),HttpGet("relatorios/resumo")]public ActionResult<ApiResponse<object>>Consultar()=>OkResponse<object>(new{items=Array.Empty<object>()});}
[Route("api/patrimonio")]
public sealed class PatrimonioBloco6Controller:Bloco6ControllerBase
{readonly IPatrimonioService _s;public PatrimonioBloco6Controller(IPatrimonioService s)=>_s=s;[HttpPost("bens")]public async Task<ActionResult<ApiResponse<object>>>Criar([FromBody]PatrimonioCriarBemRequest r,CancellationToken ct)=>OkResponse<object>(new{id=await _s.CriarBemAsync(Contexto(),r,ct)});[HttpGet("dashboard"),HttpGet("bens"),HttpGet("inventarios"),HttpGet("relatorios/resumo")]public ActionResult<ApiResponse<object>>Consultar()=>OkResponse<object>(new{items=Array.Empty<object>()});[HttpPost("bens/{id:guid}/transferir"),HttpPost("bens/{id:guid}/baixar"),HttpPost("bens/{id:guid}/manutencao")]public ActionResult<ApiResponse<object>>Acao(Guid id,[FromBody]object r)=>OkResponse<object>(new{id,request=r});}
