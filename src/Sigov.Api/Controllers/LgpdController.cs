using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/lgpd")]
public sealed class LgpdController : ControllerBase
{
    [HttpGet("dashboard")]
    public ActionResult<ApiResponse<object>> Dashboard() => ApiResponse<object>.Ok(new { solicitacoesVencendo = 0, incidentesAbertos = 0, consentimentosAtivos = 0 });

    [HttpGet("solicitacoes")]
    public ActionResult<ApiResponse<object>> Solicitacoes() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("consentimentos")]
    public ActionResult<ApiResponse<object>> Consentimentos() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });
}
