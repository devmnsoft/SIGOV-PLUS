using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class BuscaController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<BuscaController> _logger;
    public BuscaController(PostBuildSaasService service, ILogger<BuscaController> logger) { _service = service; _logger = logger; }
    [HttpGet("/Busca")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken) { try { return View(await _service.BuscarAsync(q, cancellationToken).ConfigureAwait(false)); } catch (Exception ex) { _logger.LogError(ex, "Falha busca global"); return View(new Sigov.Web.Models.PostBuild.GlobalSearchViewModel { Query = q ?? string.Empty, MensagemFallback = "Busca indisponível no momento." }); } }
}
