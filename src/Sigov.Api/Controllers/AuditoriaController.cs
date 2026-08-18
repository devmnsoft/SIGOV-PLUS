using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auditoria")]
public sealed class AuditoriaController : ControllerBase
{
    [HttpGet("trilhas")]
    public ActionResult<ApiResponse<object>> Trilhas() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("dados-pessoais")]
    public ActionResult<ApiResponse<object>> AcessosDadosPessoais() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), masked = true });

    [HttpGet("timeline")]
    public ActionResult<ApiResponse<object>> Timeline([FromQuery] string chave = "") => ApiResponse<object>.Ok(new { chave, items = Array.Empty<object>() });

    [HttpGet("dashboard")]
    public ActionResult<ApiResponse<object>> Dashboard() => ApiResponse<object>.Ok(new { eventos = 0, falhasAcesso = 0, exportacoes = 0 });

    [HttpGet("eventos")]
    public ActionResult<ApiResponse<object>> Eventos() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), beforeAfter = true });

    [HttpGet("exportacoes")]
    public ActionResult<ApiResponse<object>> Exportacoes() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), masked = true });

    [HttpGet("falhas-acesso")]
    public ActionResult<ApiResponse<object>> FalhasAcesso() => ApiResponse<object>.Ok(new { items = Array.Empty<object>() });
}
