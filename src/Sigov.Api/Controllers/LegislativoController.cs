using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController,Authorize,Route("api/legislativo")]
public sealed class LegislativoController : ControllerBase
{
 private readonly ILegislativoService _service; public LegislativoController(ILegislativoService service)=>_service=service;
 [HttpGet("dashboard")]public async Task<ActionResult<ApiResponse<Bloco8DashboardDto>>> Dashboard(CancellationToken ct)=>From(await _service.DashboardAsync("legislativo_proposicao",ct));
 [HttpGet("proposicoes")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> List([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("legislativo_proposicao",pagina,tamanho,ct));
 [HttpPost("proposicoes")]public async Task<ActionResult<ApiResponse<long>>> Create([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("legislativo_proposicao",request,HttpContext.TraceIdentifier,ct));
 [HttpGet("sessoes")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> ListSessao([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("legislativo_sessao",pagina,tamanho,ct));
 [HttpPost("sessoes")]public async Task<ActionResult<ApiResponse<long>>> CreateSessao([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("legislativo_sessao",request,HttpContext.TraceIdentifier,ct));
 [HttpGet("normas")]public async Task<ActionResult<ApiResponse<PagedResult<Bloco8RegistroDto>>>> ListNorma([FromQuery]int pagina=1,[FromQuery]int tamanho=20,CancellationToken ct=default)=>From(await _service.ListarAsync("legislativo_norma",pagina,tamanho,ct));
 [HttpPost("normas")]public async Task<ActionResult<ApiResponse<long>>> CreateNorma([FromBody]Bloco8RegistroRequest request,CancellationToken ct)=>From(await _service.CriarAsync("legislativo_norma",request,HttpContext.TraceIdentifier,ct));
 private ActionResult<ApiResponse<T>> From<T>(Result<T> result)=>result.IsSuccess?Ok(ApiResponse<T>.Ok(result.Value!,correlationId:HttpContext.TraceIdentifier)):BadRequest(ApiResponse<T>.Fail(result.Error??"Falha na operação.",HttpContext.TraceIdentifier));
}
