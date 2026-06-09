using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(PostBuildSaasService service, ILogger<DashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await _service.CriarDashboardAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha tratada ao abrir dashboard.");
            return View(await _service.CriarDashboardAsync(cancellationToken).ConfigureAwait(false));
        }
    }
}
