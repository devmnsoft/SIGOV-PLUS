using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Executive;

namespace Sigov.Web.Controllers;

public sealed class ExecutivoController : Controller
{
    private readonly IExecutiveDashboardService _dashboardService;

    public ExecutivoController(IExecutiveDashboardService dashboardService) => _dashboardService = dashboardService;

    public IActionResult Index() => View(_dashboardService.GetDashboard());
}
