using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MinhaCentralController : Controller
{
    private readonly MinhaCentralService _service;
    private readonly ILogger<MinhaCentralController> _logger;
    private readonly SavedFilterService _savedFilters;
    private readonly Sigov.Application.Abstractions.ICurrentTenant _tenant;
    private readonly Sigov.Application.Abstractions.ICurrentUser _user;

    public MinhaCentralController(MinhaCentralService service, ILogger<MinhaCentralController> logger, SavedFilterService savedFilters, Sigov.Application.Abstractions.ICurrentTenant tenant, Sigov.Application.Abstractions.ICurrentUser user)
    {
        _service = service;
        _logger = logger;
        _savedFilters = savedFilters;
        _tenant = tenant;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, int pagina = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var savedFilters = await _savedFilters.ListAsync(_tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant obrigatório."), _user.UsuarioId ?? throw new UnauthorizedAccessException("Usuário obrigatório."), "minha-central", cancellationToken).ConfigureAwait(false);
            status ??= savedFilters.FirstOrDefault(x => x.IsDefault)?.Status;
            var model = await _service.ObterResumoAsync(User, status, pagina, cancellationToken).ConfigureAwait(false);
            ViewBag.FiltrosSalvos = savedFilters;
            ViewBag.Status = status;
            _logger.LogInformation("Minha Central acessada. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return View(model);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Acesso negado à Minha Central. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View(new Sigov.Web.Models.PostBuild.MinhaCentralViewModel
            {
                Estado = "negado",
                MensagemFallback = $"O contexto atual não permite consultar esta central. Referência: {HttpContext.TraceIdentifier}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha tratada ao abrir Minha Central. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new Sigov.Web.Models.PostBuild.MinhaCentralViewModel
            {
                Estado = "indisponivel",
                MensagemFallback = $"Central indisponível. Nenhuma pendência foi simulada. Referência: {HttpContext.TraceIdentifier}."
            });
        }
    }
}
