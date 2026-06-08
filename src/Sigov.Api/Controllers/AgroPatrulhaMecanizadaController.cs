using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.PatrulhaMecanizada;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro")]
public sealed class AgroPatrulhaMecanizadaController : ControllerBase
{
    private readonly IAgroMaquinaService _maquinas; private readonly IAgroImplementoService _implementos; private readonly IAgroAgendaMaquinaService _agenda; private readonly IAgroServicoMaquinaService _servicos;
    public AgroPatrulhaMecanizadaController(IAgroMaquinaService maquinas,IAgroImplementoService implementos,IAgroAgendaMaquinaService agenda,IAgroServicoMaquinaService servicos){_maquinas=maquinas;_implementos=implementos;_agenda=agenda;_servicos=servicos;}
    [HttpGet("api/agro/patrulha-mecanizada/resumo")] public async Task<ActionResult<ApiResponse<AgroPatrulhaResumoResponse>>> Resumo(CancellationToken ct)=>FromResult(await _maquinas.ResumoAsync(ct));
    [HttpGet("api/agro/maquinas")] public async Task<ActionResult<ApiResponse<PagedResult<AgroMaquinaResponse>>>> Maquinas([FromQuery] AgroMaquinaFiltro filtro,CancellationToken ct)=>FromResult(await _maquinas.ListarAsync(filtro,ct));
    [HttpGet("api/agro/maquinas/{id:long}")] public async Task<ActionResult<ApiResponse<AgroMaquinaResponse>>> Maquina(long id,CancellationToken ct)=>FromResult(await _maquinas.ObterAsync(id,ct));
    [HttpPost("api/agro/maquinas")] public async Task<ActionResult<ApiResponse<long>>> CriarMaquina([FromBody] AgroMaquinaCreateRequest request,CancellationToken ct)=>FromResult(await _maquinas.CriarAsync(request,ct));
    [HttpPut("api/agro/maquinas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarMaquina(long id,[FromBody] AgroMaquinaUpdateRequest request,CancellationToken ct)=>FromResult(await _maquinas.AtualizarAsync(id,request,ct));
    [HttpDelete("api/agro/maquinas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> ExcluirMaquina(long id,CancellationToken ct)=>FromResult(await _maquinas.ExcluirAsync(id,ct));
    [HttpGet("api/agro/implementos")] public async Task<ActionResult<ApiResponse<PagedResult<AgroImplementoResponse>>>> Implementos([FromQuery] AgroMaquinaFiltro filtro,CancellationToken ct)=>FromResult(await _implementos.ListarAsync(filtro,ct));
    [HttpPost("api/agro/implementos")] public async Task<ActionResult<ApiResponse<long>>> CriarImplemento([FromBody] AgroImplementoCreateRequest request,CancellationToken ct)=>FromResult(await _implementos.CriarAsync(request,ct));
    [HttpGet("api/agro/maquinas/agenda")] public async Task<ActionResult<ApiResponse<PagedResult<AgroAgendaMaquinaResponse>>>> Agenda([FromQuery] AgroMaquinaFiltro filtro,CancellationToken ct)=>FromResult(await _agenda.ListarAsync(filtro,ct));
    [HttpPost("api/agro/maquinas/agenda")] public async Task<ActionResult<ApiResponse<long>>> CriarAgenda([FromBody] AgroAgendaMaquinaCreateRequest request,CancellationToken ct)=>FromResult(await _agenda.CriarAsync(request,ct));
    [HttpGet("api/agro/servicos-maquina")] public async Task<ActionResult<ApiResponse<PagedResult<AgroServicoMaquinaResponse>>>> Servicos([FromQuery] AgroServicoMaquinaFiltro filtro,CancellationToken ct)=>FromResult(await _servicos.ListarAsync(filtro,ct));
    [HttpGet("api/agro/servicos-maquina/{id:long}")] public async Task<ActionResult<ApiResponse<AgroServicoMaquinaResponse>>> Servico(long id,CancellationToken ct)=>FromResult(await _servicos.ObterAsync(id,ct));
    [HttpPost("api/agro/servicos-maquina")] public async Task<ActionResult<ApiResponse<long>>> CriarServico([FromBody] AgroServicoMaquinaCreateRequest request,CancellationToken ct)=>FromResult(await _servicos.CriarAsync(request,ct));
    [HttpPut("api/agro/servicos-maquina/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarServico(long id,[FromBody] AgroServicoMaquinaUpdateRequest request,CancellationToken ct)=>FromResult(await _servicos.AtualizarAsync(id,request,ct));
    [HttpPost("api/agro/servicos-maquina/{id:long}/agendar")] public async Task<ActionResult<ApiResponse<object>>> Agendar(long id,[FromBody] AgendarServicoMaquinaRequest request,CancellationToken ct)=>FromResult(await _servicos.AgendarAsync(id,request,ct));
    [HttpPost("api/agro/servicos-maquina/{id:long}/executar")] public async Task<ActionResult<ApiResponse<object>>> Executar(long id,[FromBody] ExecutarServicoMaquinaRequest request,CancellationToken ct)=>FromResult(await _servicos.ExecutarAsync(id,request,ct));
    [HttpPost("api/agro/servicos-maquina/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id,[FromBody] CancelarServicoMaquinaRequest request,CancellationToken ct)=>FromResult(await _servicos.CancelarAsync(id,request,ct));
    [HttpDelete("api/agro/servicos-maquina/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> ExcluirServico(long id,CancellationToken ct)=>FromResult(await _servicos.ExcluirAsync(id,ct));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> r){ if(r.IsSuccess&&r.Value is not null)return Ok(ApiResponse<T>.Ok(r.Value)); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<T>.Fail(r.Error)); if(r.Error?.Contains("não encontr",StringComparison.OrdinalIgnoreCase)==true)return NotFound(ApiResponse<T>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<T>.Fail(r.Error??"Requisição inválida.")); }
    private ActionResult<ApiResponse<object>> FromResult(Result r){ if(r.IsSuccess)return Ok(ApiResponse<object>.Ok(new{ok=true})); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<object>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<object>.Fail(r.Error??"Requisição inválida.")); }
}
