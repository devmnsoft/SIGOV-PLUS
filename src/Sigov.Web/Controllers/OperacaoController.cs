using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class OperacaoController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<OperacaoController> _logger;

    public OperacaoController(PostBuildSaasService service, ILogger<OperacaoController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("/Health")]
    [HttpGet("/Operacao/Health")]
    public IActionResult Health()
    {
        try
        {
            return View(new HealthVisualViewModel
            {
                Itens = _service.CriarAmbiente(true),
                Ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                Versao = "0.1.0",
                ServerTime = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela visual de health.");
            return View(new HealthVisualViewModel { Itens = _service.CriarAmbiente(false), MensagemFallback = "Falha ao consultar saúde completa do ambiente." });
        }
    }
}
