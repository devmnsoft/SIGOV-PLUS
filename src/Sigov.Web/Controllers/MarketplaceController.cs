using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class MarketplaceController : Controller
{
    private readonly PostBuildSaasService _service; private readonly ILogger<MarketplaceController> _logger;
    public MarketplaceController(PostBuildSaasService service, ILogger<MarketplaceController> logger) { _service = service; _logger = logger; }
    [HttpGet("/Marketplace")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.ListarModulosAsync(null, cancellationToken).ConfigureAwait(false));
    [HttpGet("/Marketplace/{categoria}")]
    public async Task<IActionResult> Categoria(string categoria, CancellationToken cancellationToken) { ViewBag.Categoria = categoria; return View("Index", await _service.ListarModulosAsync(null, cancellationToken).ConfigureAwait(false)); }
    [HttpGet("/Marketplace/Modulo/{codigo}")]
    public async Task<IActionResult> Modulo(string codigo, CancellationToken cancellationToken)
    {
        var modulos = await _service.ListarModulosAsync(null, cancellationToken).ConfigureAwait(false);
        return View("Index", modulos.Where(m => string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(new ModuleViewModel(codigo, codigo, SigovFeatureStatus.EmImplantacao, "Módulo solicitado sem catálogo persistente.")).ToArray());
    }
}
