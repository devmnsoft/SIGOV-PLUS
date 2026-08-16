using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;
using Sigov.Application.Obras;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/obras")]
public sealed class ObrasController : ControllerBase
{
    private readonly IObrasService _service;public ObrasController(IObrasService service)=>_service=service;
    [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<ObrasDashboardDto>>> Dashboard(CancellationToken ct)=>Response(await _service.DashboardAsync(ct));
    [HttpGet]public async Task<ActionResult<ApiResponse<PagedResult<ObraRegistroDto>>>> Obras([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>Response(await _service.ListarAsync("obras",null,pagina,tamanho,ct));
    [HttpPost]public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]ObraRegistroRequest request,CancellationToken ct)=>Response(await _service.CriarAsync("obras",request,HttpContext.TraceIdentifier,ct));
    [HttpGet("{obraId:long}/{recurso:regex(^etapas|medicoes|fiscalizacoes|diario$)}")]public async Task<ActionResult<ApiResponse<PagedResult<ObraRegistroDto>>>> Listar(long obraId,string recurso,[FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>Response(await _service.ListarAsync(recurso,obraId,pagina,tamanho,ct));
    [HttpPost("{obraId:long}/{recurso:regex(^etapas|medicoes|fiscalizacoes|diario$)}")]public async Task<ActionResult<ApiResponse<long>>> CriarItem(long obraId,string recurso,[FromBody]ObraRegistroRequest request,CancellationToken ct)=>Response(await _service.CriarAsync(recurso,request with{ObraId=obraId},HttpContext.TraceIdentifier,ct));
    [HttpGet("relatorios/resumo")]public async Task<ActionResult<ApiResponse<ObrasDashboardDto>>> Relatorio(CancellationToken ct)=>Response(await _service.DashboardAsync(ct));
    private ActionResult<ApiResponse<T>> Response<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação."));
}
