using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Executive;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/executivo/dashboard")]
public sealed class ExecutivoDashboardController : ControllerBase
{
    private readonly IExecutiveDashboardService _dashboardService;

    public ExecutivoDashboardController(IExecutiveDashboardService dashboardService) => _dashboardService = dashboardService;

    [HttpGet]
    public ActionResult<ApiResponse<ExecutiveDashboardResponse>> Get() => Ok(ApiResponse<ExecutiveDashboardResponse>.Ok(_dashboardService.GetDashboard()));
}
