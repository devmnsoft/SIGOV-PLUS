using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Programas;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro")]
public sealed class AgroProgramasController : ControllerBase
{
    private readonly IAgroProgramaRuralService _programas; private readonly IAgroBeneficioRuralService _beneficios; private readonly IAgroInsumoService _insumos; private readonly IAgroDistribuicaoInsumoService _distribuicoes;
    public AgroProgramasController(IAgroProgramaRuralService programas, IAgroBeneficioRuralService beneficios, IAgroInsumoService insumos, IAgroDistribuicaoInsumoService distribuicoes){_programas=programas;_beneficios=beneficios;_insumos=insumos;_distribuicoes=distribuicoes;}
    [HttpGet("api/agro/programas")] public async Task<ActionResult<ApiResponse<PagedResult<AgroProgramaRuralResponse>>>> Programas([FromQuery] AgroProgramaRuralFiltro filtro,CancellationToken ct)=>FromResult(await _programas.ListarAsync(filtro,ct));
    [HttpGet("api/agro/programas/{id:long}")] public async Task<ActionResult<ApiResponse<AgroProgramaRuralResponse>>> Programa(long id,CancellationToken ct)=>FromResult(await _programas.ObterAsync(id,ct));
    [HttpPost("api/agro/programas")] public async Task<ActionResult<ApiResponse<long>>> CriarPrograma([FromBody] AgroProgramaRuralCreateRequest request,CancellationToken ct)=>FromResult(await _programas.CriarAsync(request,ct));
    [HttpPut("api/agro/programas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarPrograma(long id,[FromBody] AgroProgramaRuralUpdateRequest request,CancellationToken ct)=>FromResult(await _programas.AtualizarAsync(id,request,ct));
    [HttpDelete("api/agro/programas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> ExcluirPrograma(long id,CancellationToken ct)=>FromResult(await _programas.ExcluirAsync(id,ct));
    [HttpGet("api/agro/beneficios")] public async Task<ActionResult<ApiResponse<PagedResult<AgroBeneficioRuralResponse>>>> Beneficios([FromQuery] AgroProgramaRuralFiltro filtro,CancellationToken ct)=>FromResult(await _beneficios.ListarAsync(filtro,ct));
    [HttpGet("api/agro/beneficios/{id:long}")] public async Task<ActionResult<ApiResponse<AgroBeneficioRuralResponse>>> Beneficio(long id,CancellationToken ct)=>FromResult(await _beneficios.ObterAsync(id,ct));
    [HttpPost("api/agro/beneficios")] public async Task<ActionResult<ApiResponse<long>>> CriarBeneficio([FromBody] AgroBeneficioRuralCreateRequest request,CancellationToken ct)=>FromResult(await _beneficios.CriarAsync(request,ct));
    [HttpPut("api/agro/beneficios/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarBeneficio(long id,[FromBody] AgroBeneficioRuralUpdateRequest request,CancellationToken ct)=>FromResult(await _beneficios.AtualizarAsync(id,request,ct));
    [HttpDelete("api/agro/beneficios/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> ExcluirBeneficio(long id,CancellationToken ct)=>FromResult(await _beneficios.ExcluirAsync(id,ct));
    [HttpGet("api/agro/beneficios/concessoes")] public async Task<ActionResult<ApiResponse<PagedResult<AgroBeneficioConcessaoResponse>>>> Concessoes([FromQuery] AgroBeneficioConcessaoFiltro filtro,CancellationToken ct)=>FromResult(await _beneficios.ListarConcessoesAsync(filtro,ct));
    [HttpGet("api/agro/beneficios/concessoes/{id:long}")] public async Task<ActionResult<ApiResponse<AgroBeneficioConcessaoResponse>>> Concessao(long id,CancellationToken ct)=>FromResult(await _beneficios.ObterConcessaoAsync(id,ct));
    [HttpPost("api/agro/beneficios/concessoes")] public async Task<ActionResult<ApiResponse<long>>> CriarConcessao([FromBody] AgroBeneficioConcessaoCreateRequest request,CancellationToken ct)=>FromResult(await _beneficios.SolicitarConcessaoAsync(request,ct));
    [HttpPost("api/agro/beneficios/concessoes/{id:long}/autorizar")] public async Task<ActionResult<ApiResponse<object>>> Autorizar(long id,[FromBody] AutorizarBeneficioRuralRequest request,CancellationToken ct)=>FromResult(await _beneficios.AutorizarAsync(id,request,ct));
    [HttpPost("api/agro/beneficios/concessoes/{id:long}/entregar")] public async Task<ActionResult<ApiResponse<object>>> Entregar(long id,[FromBody] EntregarBeneficioRuralRequest request,CancellationToken ct)=>FromResult(await _beneficios.EntregarAsync(id,request,ct));
    [HttpPost("api/agro/beneficios/concessoes/{id:long}/indeferir")] public async Task<ActionResult<ApiResponse<object>>> Indeferir(long id,[FromBody] IndeferirBeneficioRuralRequest request,CancellationToken ct)=>FromResult(await _beneficios.IndeferirAsync(id,request,ct));
    [HttpPost("api/agro/beneficios/concessoes/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id,[FromBody] CancelarBeneficioRuralRequest request,CancellationToken ct)=>FromResult(await _beneficios.CancelarAsync(id,request,ct));
    [HttpGet("api/agro/insumos")] public async Task<ActionResult<ApiResponse<PagedResult<AgroInsumoResponse>>>> Insumos([FromQuery] AgroProgramaRuralFiltro filtro,CancellationToken ct)=>FromResult(await _insumos.ListarAsync(filtro,ct));
    [HttpPost("api/agro/insumos")] public async Task<ActionResult<ApiResponse<long>>> CriarInsumo([FromBody] AgroInsumoCreateRequest request,CancellationToken ct)=>FromResult(await _insumos.CriarAsync(request,ct));
    [HttpGet("api/agro/insumos/distribuicoes")] public async Task<ActionResult<ApiResponse<PagedResult<AgroDistribuicaoInsumoResponse>>>> Distribuicoes([FromQuery] AgroBeneficioConcessaoFiltro filtro,CancellationToken ct)=>FromResult(await _distribuicoes.ListarAsync(filtro,ct));
    [HttpPost("api/agro/insumos/distribuicoes")] public async Task<ActionResult<ApiResponse<long>>> CriarDistribuicao([FromBody] AgroDistribuicaoInsumoCreateRequest request,CancellationToken ct)=>FromResult(await _distribuicoes.CriarAsync(request,ct));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> r){ if(r.IsSuccess&&r.Value is not null)return Ok(ApiResponse<T>.Ok(r.Value)); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<T>.Fail(r.Error)); if(r.Error?.Contains("não encontr",StringComparison.OrdinalIgnoreCase)==true)return NotFound(ApiResponse<T>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<T>.Fail(r.Error??"Requisição inválida.")); }
    private ActionResult<ApiResponse<object>> FromResult(Result r){ if(r.IsSuccess)return Ok(ApiResponse<object>.Ok(new{ok=true})); if(r.Error=="403")return Forbid(); if(r.Error?.Contains("autenticado",StringComparison.OrdinalIgnoreCase)==true)return Unauthorized(ApiResponse<object>.Fail(r.Error)); return UnprocessableEntity(ApiResponse<object>.Fail(r.Error??"Requisição inválida.")); }
}
