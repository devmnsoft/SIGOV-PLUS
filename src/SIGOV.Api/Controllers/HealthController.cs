using Microsoft.AspNetCore.Mvc;
using SIGOV.Api.Contracts;

namespace SIGOV.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<object>> Get()
    {
        return Ok(ApiResponse<object>.Ok(new { status = "Healthy", service = "SIGOV.Api" }));
    }
}
