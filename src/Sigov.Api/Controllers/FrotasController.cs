using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;
using Sigov.Application.Frotas;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/frotas")]
public sealed class FrotasController : ControllerBase
{
    private readonly IFrotasService _service;public FrotasController(IFrotasService service)=>_service=service;
    [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<FrotasDashboardDto>>> Dashboard(CancellationToken ct)=>FromResult(await _service.DashboardAsync(ct));
    [HttpGet("{recurso:regex(^veiculos|motoristas|abastecimentos|manutencoes|viagens$)}")]public async Task<ActionResult<ApiResponse<PagedResult<FrotaRegistroDto>>>> Listar(string recurso,[FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>FromResult(await _service.ListarAsync(recurso,pagina,tamanho,ct));
    [HttpPost("{recurso:regex(^veiculos|motoristas|abastecimentos|manutencoes|viagens$)}")]public async Task<ActionResult<ApiResponse<long>>> Criar(string recurso,[FromBody]FrotaRegistroRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(recurso,request,HttpContext.TraceIdentifier,ct));
    [HttpGet("relatorios/resumo")]public async Task<ActionResult<ApiResponse<FrotasDashboardDto>>> Relatorio(CancellationToken ct)=>FromResult(await _service.DashboardAsync(ct));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!,correlationId:HttpContext.TraceIdentifier)):result.Error=="403"?StatusCode(StatusCodes.Status403Forbidden,ApiResponse<T>.Fail("Acesso negado.",HttpContext.TraceIdentifier)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação.",HttpContext.TraceIdentifier));
}
