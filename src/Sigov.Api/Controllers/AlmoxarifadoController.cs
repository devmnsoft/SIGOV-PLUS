using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Almoxarifado;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Api.Controllers;
[ApiController,Authorize,Route("api/almoxarifado")]
public sealed class AlmoxarifadoController(IAlmoxarifadoService service,ICurrentTenant tenant,ICurrentUser user,IAuthorizationEvaluator auth):ControllerBase
{
 [HttpGet("dashboard")]public Task<IActionResult> Dashboard(CancellationToken ct)=>Run(AlmoxarifadoPermissoes.DashboardVisualizar,()=>service.ObterDashboardAsync(T(),E(),ct),ct);
 [HttpGet("materiais")]public Task<IActionResult> Materiais([FromQuery]AlmoxarifadoFiltro f,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.MaterialVisualizar,()=>service.ListarMateriaisAsync(T(),E(),f,ct),ct);
 [HttpPost("materiais")]public Task<IActionResult> Material(MaterialInput i,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.MaterialCriar,()=>service.CriarMaterialAsync(T(),U(),Trace(),i,ct),ct);
 [HttpPost("movimentacoes/entrada")]public Task<IActionResult> Entrada(MovimentacaoInput i,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.Entrada,async()=>{await service.RegistrarEntradaAsync(T(),U(),Trace(),i,ct);return new{registrada=true};},ct);
 [HttpPost("movimentacoes/saida")]public Task<IActionResult> Saida(MovimentacaoInput i,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.Saida,async()=>{await service.RegistrarSaidaAsync(T(),U(),Trace(),i,ct);return new{registrada=true};},ct);
 [HttpGet("requisicoes")]public Task<IActionResult> Requisicoes(string? status,int pagina=1,CancellationToken ct=default)=>Run(AlmoxarifadoPermissoes.RequisicaoVisualizar,()=>service.ListarRequisicoesAsync(T(),E(),status,pagina,ct),ct);
 [HttpPost("requisicoes")]public Task<IActionResult> Requisicao(RequisicaoInput i,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.RequisicaoCriar,()=>service.CriarRequisicaoAsync(T(),U(),Trace(),i,ct),ct);
 [HttpPost("requisicoes/{id:long}/{acao}")]public Task<IActionResult> Status(long id,string acao,[FromBody]JustificativaInput? i,CancellationToken ct){var p=acao.ToLowerInvariant() switch{"aprovar" or "rejeitar"=>AlmoxarifadoPermissoes.RequisicaoAprovar,"atender"=>AlmoxarifadoPermissoes.RequisicaoAtender,_=>AlmoxarifadoPermissoes.RequisicaoCriar};return Run(p,async()=>{await service.AlterarStatusAsync(T(),E(),U(),Trace(),id,acao,i?.Justificativa,ct);return new{id,status=acao};},ct);}
 [HttpPost("requisicoes/{id:long}/separacoes")]public Task<IActionResult> Separar(long id,SeparacaoInput input,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.RequisicaoAtender,()=>service.SepararAsync(T(),E(),U(),Trace(),id,input,ct),ct);
 [HttpGet("entregas/{id:long}")]public Task<IActionResult> Entrega(long id,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.RequisicaoVisualizar,async()=>await service.ObterEntregaAsync(T(),E(),id,ct)??throw new KeyNotFoundException("Entrega não encontrada."),ct);
 [HttpPost("entregas/{id:long}/expedicao")]public Task<IActionResult> Expedir(long id,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.RequisicaoAtender,async()=>{await service.ExpedirAsync(T(),E(),U(),Trace(),id,ct);return new{id,status="EXPEDIDA"};},ct);
 [HttpPost("entregas/{id:long}/recebimento")]public Task<IActionResult> Receber(long id,RecebimentoInput input,CancellationToken ct)=>Run(AlmoxarifadoPermissoes.RecebimentoConfirmar,async()=>{await service.ReceberAsync(T(),E(),U(),Trace(),id,input,ct);return new{id,registrado=true};},ct);
 async Task<IActionResult> Run<TV>(string p,Func<Task<TV>> fn,CancellationToken ct){if(!await Allowed(p,ct))return Forbid();try{return Ok(ApiResponse<TV>.Ok(await fn()));}catch(KeyNotFoundException x){return NotFound(ApiResponse<TV>.Fail(x.Message));}catch(ArgumentException x){return BadRequest(ApiResponse<TV>.Fail(x.Message));}catch(InvalidOperationException x){return Conflict(ApiResponse<TV>.Fail(x.Message));}}
 async Task<bool> Allowed(string p,CancellationToken ct){var i=p.LastIndexOf('.');return(await auth.EvaluateAsync(new(U(),"almoxarifado",p[..i],p[(i+1)..],T(),E(),tenant.ExercicioId,null,null,Trace(),"API_FUNC02"),ct)).Permitido;}
 long T()=>tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório.");long E()=>tenant.EntidadeId??throw new InvalidOperationException("entidade_id obrigatório.");long U()=>user.UsuarioId??throw new InvalidOperationException("Usuário obrigatório.");string Trace()=>HttpContext.TraceIdentifier;
 public sealed record JustificativaInput(string? Justificativa);
}
