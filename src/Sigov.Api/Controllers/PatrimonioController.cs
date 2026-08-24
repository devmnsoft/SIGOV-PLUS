using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Common;
using Sigov.Application.Patrimonio;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, Route("api/patrimonio")]
public sealed class PatrimonioController(IPatrimonioService service, ICurrentTenant tenant, ICurrentUser user, IAuthorizationEvaluator authorization) : ControllerBase
{
    [HttpGet("bens")] public async Task<IActionResult> Bens([FromQuery] PatrimonioBemFiltro filtro,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemVisualizar,()=>service.ListarBensAsync(Tenant(),filtro,ct),ct);
    [HttpGet("bens/{id:long}")] public async Task<IActionResult> Bem(long id,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemVisualizar,async()=>await service.ObterBemAsync(Tenant(),id,ct)??throw new KeyNotFoundException("Bem não encontrado."),ct);
    [HttpPost("bens")] public async Task<IActionResult> Criar(PatrimonioBemInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemCriar,()=>service.CriarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,input,ct),ct);
    [HttpPut("bens/{id:long}")] public async Task<IActionResult> Editar(long id,PatrimonioBemInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemEditar,async()=>{await service.EditarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);return new{id};},ct);
    [HttpPost("bens/{id:long}/movimentar")] public async Task<IActionResult> Movimentar(long id,PatrimonioMovimentacaoInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemMovimentar,async()=>{await service.MovimentarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);return new{id};},ct);
    [HttpPost("bens/{id:long}/baixar")] public async Task<IActionResult> Baixar(long id,PatrimonioBaixaInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.BemBaixar,async()=>{await service.BaixarBemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,input,ct);return new{id};},ct);
    [HttpGet("inventarios")] public async Task<IActionResult> Inventarios(int pagina=1,int tamanho=25,CancellationToken ct=default)=>await Executar(PatrimonioPermissoes.InventarioVisualizar,()=>service.ListarInventariosAsync(Tenant(),pagina,tamanho,ct),ct);
    [HttpGet("inventarios/{id:long}")] public async Task<IActionResult> Inventario(long id,CancellationToken ct)=>await Executar(PatrimonioPermissoes.InventarioVisualizar,async()=>await service.ObterInventarioAsync(Tenant(),id,ct)??throw new KeyNotFoundException("Inventário não encontrado."),ct);
    [HttpPost("inventarios")] public async Task<IActionResult> Abrir(PatrimonioInventarioInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.InventarioCriar,()=>service.AbrirInventarioAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,input,ct),ct);
    [HttpPost("inventarios/{id:long}/itens/{itemId:long}/conferir")] public async Task<IActionResult> Conferir(long id,long itemId,PatrimonioConferenciaInput input,CancellationToken ct)=>await Executar(PatrimonioPermissoes.InventarioConferir,async()=>{await service.ConferirItemAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,itemId,input,ct);return new{id,itemId};},ct);
    [HttpPost("inventarios/{id:long}/fechar")] public async Task<IActionResult> Fechar(long id,CancellationToken ct)=>await Executar(PatrimonioPermissoes.InventarioCriar,async()=>{await service.FecharInventarioAsync(Tenant(),Usuario(),HttpContext.TraceIdentifier,id,ct);return new{id};},ct);
    [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct)=>await Executar(PatrimonioPermissoes.DashboardVisualizar,()=>service.ObterDashboardAsync(Tenant(),ct),ct);
    [HttpGet("bens/exportar.csv")] public async Task<IActionResult> Exportar([FromQuery]PatrimonioBemFiltro filtro,CancellationToken ct){if(!await Permitido(PatrimonioPermissoes.Exportar,ct))return Forbid();return File(await service.ExportarCsvAsync(Tenant(),filtro,ct),"text/csv; charset=utf-8","patrimonio-bens.csv");}

    private async Task<IActionResult> Executar<T>(string permissao,Func<Task<T>> action,CancellationToken ct){if(!await Permitido(permissao,ct))return Forbid();try{return Ok(ApiResponse<T>.Ok(await action()));}catch(KeyNotFoundException e){return NotFound(ApiResponse<T>.Fail(e.Message));}catch(ArgumentException e){return BadRequest(ApiResponse<T>.Fail(e.Message));}catch(InvalidOperationException e){return Conflict(ApiResponse<T>.Fail(e.Message));}}
    private async Task<bool> Permitido(string chave,CancellationToken ct){var split=chave.LastIndexOf('.');var recurso=chave[..split];var acao=chave[(split+1)..];var d=await authorization.EvaluateAsync(new(Usuario(),"patrimonio",recurso,acao,Tenant(),tenant.EntidadeId,tenant.ExercicioId,null,null,HttpContext.TraceIdentifier,"API_FUNC01"),ct);return d.Permitido;}
    private long Tenant()=>tenant.TenantId??throw new InvalidOperationException("tenant_id obrigatório."); private long Usuario()=>user.UsuarioId??throw new InvalidOperationException("Usuário autenticado obrigatório.");
}
