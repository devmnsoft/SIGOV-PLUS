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

    [HttpGet("/Operacao/Logs")]
    [HttpGet("/Operacao/AuditoriaTecnica")]
    [HttpGet("/Operacao/Metricas")]
    [HttpGet("/Operacao/ApiLogs")]
    [HttpGet("/Operacao/Webhooks")]
    [HttpGet("/Operacao/Outbox")]
    [HttpGet("/Operacao/Worker")]
    [HttpGet("/Operacao/MetricasFluxos")]
    [HttpGet("/Operacao/Erros")]
    [HttpGet("/Operacao/Backup")]
    public IActionResult Observabilidade() => View("Observabilidade", new OperacaoObservabilidadeViewModel(Request.Path.Value ?? "/Operacao/Logs"));

    [HttpGet("/Health")]
    [HttpGet("/Operacao/Health")]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        try
        {
            return View("Health", new HealthVisualViewModel
            {
                Itens = await _service.VerificarAmbienteAsync(cancellationToken).ConfigureAwait(false),
                Ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                Versao = "0.1.0",
                ServerTime = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela visual de health.");
            return View("Health", new HealthVisualViewModel { Itens = _service.CriarAmbiente(false), MensagemFallback = "Falha ao consultar saúde completa do ambiente." });
        }
    }

    [Authorize(Roles = "Admin,Administrador")]
    [HttpGet("/Operacao/Runtime")]
    public Task<IActionResult> Runtime(CancellationToken cancellationToken) => Health(cancellationToken);

    [Authorize(Roles = "Admin,Administrador")]
    [HttpGet("/Operacao/Diagnostico")]
    public IActionResult Diagnostico() => View();
}

public sealed record OperacaoObservabilidadeViewModel(string Rota);
