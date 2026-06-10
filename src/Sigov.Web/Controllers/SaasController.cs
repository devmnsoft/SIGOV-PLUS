using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaasController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<SaasController> _logger;

    public SaasController(PostBuildSaasService service, ILogger<SaasController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Tenants(string? busca, CancellationToken cancellationToken)
    {
        try
        {
            var tenants = await _service.ListarTenantsAsync(busca, cancellationToken).ConfigureAwait(false);
            return View(new TenantsViewModel { Tenants = tenants, Busca = busca ?? string.Empty });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela de tenants.");
            return View(new TenantsViewModel { MensagemFallback = "Não foi possível consultar tenants agora." });
        }
    }

    [HttpGet]
    public IActionResult Planos() => View();

    [HttpGet]
    public IActionResult Implantacao(long? tenantId) => View(tenantId ?? 0);

    [HttpGet]
    public IActionResult Parametros(long? tenantId) => View(tenantId ?? 0);

    [HttpGet("Tenants/{id:long}/Assinatura")]
    public IActionResult Assinatura(long id) => View(id);

    [HttpGet]
    public async Task<IActionResult> Modulos(long? tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var modulos = await _service.ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return View(new ModulosSaasViewModel { TenantId = tenantId ?? 0, Modulos = modulos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela de módulos SaaS.");
            return View(new ModulosSaasViewModel { MensagemFallback = "Não foi possível consultar módulos agora." });
        }
    }
}
