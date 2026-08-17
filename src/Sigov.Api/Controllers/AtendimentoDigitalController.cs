using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/atendimento-digital")]
public sealed class AtendimentoDigitalController : ControllerBase
{
 private readonly IAtendimentoDigitalService _service; public AtendimentoDigitalController(IAtendimentoDigitalService service)=>_service=service;
 [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco8DashboardDto>>> Dashboard(CancellationToken ct)=>From(await _service.DashboardAsync("atendimento_digital_chamado",ct));
 [HttpGet("chamados")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> List([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("atendimento_digital_chamado",pagina,tamanho,ct));
 [HttpPost("chamados")]public async Task<ActionResult<ApiResponse<long>>> Create([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("atendimento_digital_chamado",request,HttpContext.TraceIdentifier,ct));
 private ActionResult<ApiResponse<T>> From<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!,correlationId:HttpContext.TraceIdentifier)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação.",HttpContext.TraceIdentifier));
}
