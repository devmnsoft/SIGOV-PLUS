using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/transparencia")]
public sealed class TransparenciaController : ControllerBase
{
 private readonly ITransparenciaService _service; public TransparenciaController(ITransparenciaService service)=>_service=service;
 [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco8DashboardDto>>> Dashboard(CancellationToken ct)=>From(await _service.DashboardAsync("transparencia_item_pntp",ct));
 [HttpGet("pntp")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> List([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("transparencia_item_pntp",pagina,tamanho,ct));
 [HttpPost("pntp")]public async Task<ActionResult<ApiResponse<long>>> Create([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("transparencia_item_pntp",request,HttpContext.TraceIdentifier,ct));
 [HttpGet("pncp/eventos")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> ListPncp([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("transparencia_pncp_evento",pagina,tamanho,ct));
 [HttpPost("pncp/eventos")]public async Task<ActionResult<ApiResponse<long>>> CreatePncp([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("transparencia_pncp_evento",request,HttpContext.TraceIdentifier,ct));
 private ActionResult<ApiResponse<T>> From<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!,correlationId:HttpContext.TraceIdentifier)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação.",HttpContext.TraceIdentifier));
}
