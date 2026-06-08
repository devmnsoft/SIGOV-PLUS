using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Producao;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro"), Route("api/agro/producao")]
public sealed class AgroProducaoController : ControllerBase
{
    private readonly IAgroProducaoAgricolaService _service; public AgroProducaoController(IAgroProducaoAgricolaService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<AgroProducaoAgricolaResponse>>>> Listar([FromQuery] AgroProducaoAgricolaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<AgroProducaoAgricolaResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] AgroProducaoAgricolaCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody] AgroProducaoAgricolaCreateRequest request,CancellationToken ct)=>FromResult(await _service.AtualizarAsync(id,request,ct));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _service.ExcluirAsync(id,ct));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> r){ if(r.IsSuccess&&r.Value is not null)return Ok(ApiResponse<T>.Ok(r.Value)); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<T>.Fail(r.Error)); if(r.Error?.Contains("não encontr",StringComparison.OrdinalIgnoreCase)==true)return NotFound(ApiResponse<T>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<T>.Fail(r.Error??"Requisição inválida.")); }
    private ActionResult<ApiResponse<object>> FromResult(Result r){ if(r.IsSuccess)return Ok(ApiResponse<object>.Ok(new{ok=true})); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<object>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<object>.Fail(r.Error??"Requisição inválida.")); }
}
