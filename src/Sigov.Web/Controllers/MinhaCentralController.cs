using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MinhaCentralController : Controller
{
    private readonly MinhaCentralService _service;
    private readonly ILogger<MinhaCentralController> _logger;

    public MinhaCentralController(MinhaCentralService service, ILogger<MinhaCentralController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _service.ObterResumoAsync(User, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Minha Central acessada. Usuario={Usuario} CorrelationId={CorrelationId}", User.Identity?.Name, HttpContext.TraceIdentifier);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha tratada ao abrir Minha Central. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new Sigov.Web.Models.PostBuild.MinhaCentralViewModel
            {
                MensagemFallback = $"Central indisponível. Nenhuma pendência foi simulada. Referência: {HttpContext.TraceIdentifier}."
            });
        }
    }
}
