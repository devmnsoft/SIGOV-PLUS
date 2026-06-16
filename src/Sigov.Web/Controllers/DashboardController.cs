using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Models.PostBuild;

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
            return View(new DashboardViewModel
            {
                MensagemFallback = "Dados indisponíveis no ambiente local. Exibindo painel demonstrativo seguro.",
                Cards = new[]
                {
                    new DashboardCard("Tenants ativos", "--", "Dados indisponíveis no ambiente local.", "secondary"),
                    new DashboardCard("Usuários ativos", "--", "Dados indisponíveis no ambiente local.", "secondary"),
                    new DashboardCard("Módulos disponíveis", "18+", "Catálogo demonstrativo carregado.", "info"),
                    new DashboardCard("Planos SaaS", "6", "Starter, Gov Basic, Gov Plus, Enterprise, Business e Industrial.", "primary")
                },
                Ambiente = _service.CriarAmbiente(false)
            });
        }
    }
}
