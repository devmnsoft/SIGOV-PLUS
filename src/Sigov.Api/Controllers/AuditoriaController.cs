using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/auditoria")]
public sealed class AuditoriaController : ControllerBase
{
    [HttpGet("trilhas")]
    public ActionResult<ApiResponse<object>> Trilhas() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("dados-pessoais")]
    public ActionResult<ApiResponse<object>> AcessosDadosPessoais() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), masked = true });

    [HttpGet("timeline")]
    public ActionResult<ApiResponse<object>> Timeline([FromQuery] string chave = "") => ApiResponse<object>.Ok(new { chave, items = Array.Empty<object>() });
}
