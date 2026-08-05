using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class BuscaController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly BuscaGlobalService _buscaGlobalService;
    private readonly ILogger<BuscaController> _logger;
    public BuscaController(PostBuildSaasService service, BuscaGlobalService buscaGlobalService, ILogger<BuscaController> logger) { _service = service; _buscaGlobalService = buscaGlobalService; _logger = logger; }
    [HttpGet("/Busca/Sugestoes")]
    public async Task<IActionResult> Sugestoes(string? q, CancellationToken cancellationToken)
    {
        try
        {
            var resultados = await _buscaGlobalService.SugerirAsync(q, User, cancellationToken).ConfigureAwait(false);
            return Json(new Sigov.Web.Models.Busca.BuscaSugestoesResponse(resultados, "fallback-seguro", HttpContext.TraceIdentifier));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao retornar sugestões globais. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Json(new Sigov.Web.Models.Busca.BuscaSugestoesResponse(Array.Empty<Sigov.Web.Models.Busca.BuscaSugestaoViewModel>(), "erro", HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("/Busca")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken) { try { return View(await _service.BuscarAsync(q, cancellationToken).ConfigureAwait(false)); } catch (Exception ex) { _logger.LogError(ex, "Falha busca global"); return View(new Sigov.Web.Models.PostBuild.GlobalSearchViewModel { Query = q ?? string.Empty, MensagemFallback = "Busca indisponível no momento." }); } }
}
