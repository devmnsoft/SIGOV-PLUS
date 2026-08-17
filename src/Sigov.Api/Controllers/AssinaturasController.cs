using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/assinaturas")]
public sealed class AssinaturasController : ControllerBase
{
 private readonly IAssinaturaService _service; public AssinaturasController(IAssinaturaService service)=>_service=service;
 [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco8DashboardDto>>> Dashboard(CancellationToken ct)=>From(await _service.DashboardAsync("assinatura_documento",ct));
 [HttpGet]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> List([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("assinatura_documento",pagina,tamanho,ct));
 [HttpPost]public async Task<ActionResult<ApiResponse<long>>> Create([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("assinatura_documento",request,HttpContext.TraceIdentifier,ct));
 private ActionResult<ApiResponse<T>> From<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!,correlationId:HttpContext.TraceIdentifier)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação.",HttpContext.TraceIdentifier));
}
