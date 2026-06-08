using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Propriedades;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro")]
public sealed class AgroPropriedadesController : ControllerBase
{
    private readonly IAgroPropriedadeService _propriedades; private readonly IAgroTalhaoService _talhoes; private readonly IAgroCulturaService _culturas; private readonly IAgroSafraService _safras;
    public AgroPropriedadesController(IAgroPropriedadeService propriedades, IAgroTalhaoService talhoes, IAgroCulturaService culturas, IAgroSafraService safras){_propriedades=propriedades;_talhoes=talhoes;_culturas=culturas;_safras=safras;}
    [HttpGet("api/agro/propriedades")] public async Task<ActionResult<ApiResponse<PagedResult<AgroPropriedadeResponse>>>> Listar([FromQuery] AgroPropriedadeFiltro filtro,CancellationToken ct)=>FromResult(await _propriedades.ListarAsync(filtro,ct));
    [HttpGet("api/agro/propriedades/{id:long}")] public async Task<ActionResult<ApiResponse<AgroPropriedadeDetalheResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _propriedades.ObterAsync(id,ct));
    [HttpPost("api/agro/propriedades")] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] AgroPropriedadeCreateRequest request,CancellationToken ct)=>FromResult(await _propriedades.CriarAsync(request,ct));
    [HttpPut("api/agro/propriedades/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody] AgroPropriedadeUpdateRequest request,CancellationToken ct)=>FromResult(await _propriedades.AtualizarAsync(id,request,ct));
    [HttpDelete("api/agro/propriedades/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _propriedades.ExcluirAsync(id,ct));
    [HttpGet("api/agro/talhoes")] public async Task<ActionResult<ApiResponse<PagedResult<AgroTalhaoResponse>>>> Talhoes([FromQuery] int page=1,[FromQuery] int pageSize=20,[FromQuery] long? propriedadeId=null,CancellationToken ct=default)=>FromResult(await _talhoes.ListarAsync(page,pageSize,propriedadeId,ct));
    [HttpPost("api/agro/propriedades/{id:long}/talhoes")] public async Task<ActionResult<ApiResponse<long>>> CriarTalhao(long id,[FromBody] AgroTalhaoCreateRequest request,CancellationToken ct)=>FromResult(await _talhoes.CriarAsync(id,request,ct));
    [HttpGet("api/agro/culturas")] public async Task<ActionResult<ApiResponse<PagedResult<AgroCulturaResponse>>>> Culturas([FromQuery] int page=1,[FromQuery] int pageSize=20,CancellationToken ct=default)=>FromResult(await _culturas.ListarAsync(page,pageSize,ct));
    [HttpPost("api/agro/culturas")] public async Task<ActionResult<ApiResponse<long>>> CriarCultura([FromBody] AgroCulturaCreateRequest request,CancellationToken ct)=>FromResult(await _culturas.CriarAsync(request,ct));
    [HttpGet("api/agro/safras")] public async Task<ActionResult<ApiResponse<PagedResult<AgroSafraResponse>>>> Safras([FromQuery] int page=1,[FromQuery] int pageSize=20,CancellationToken ct=default)=>FromResult(await _safras.ListarAsync(page,pageSize,ct));
    [HttpPost("api/agro/safras")] public async Task<ActionResult<ApiResponse<long>>> CriarSafra([FromBody] AgroSafraCreateRequest request,CancellationToken ct)=>FromResult(await _safras.CriarAsync(request,ct));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> r){ if(r.IsSuccess&&r.Value is not null)return Ok(ApiResponse<T>.Ok(r.Value)); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<T>.Fail(r.Error)); if(r.Error?.Contains("não encontr",StringComparison.OrdinalIgnoreCase)==true)return NotFound(ApiResponse<T>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<T>.Fail(r.Error??"Requisição inválida.")); }
    private ActionResult<ApiResponse<object>> FromResult(Result r){ if(r.IsSuccess)return Ok(ApiResponse<object>.Ok(new{ok=true})); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<object>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<object>.Fail(r.Error??"Requisição inválida.")); }
}
