using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Health;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/operacao/health")]
public sealed class OperacaoHealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OperacaoHealthController> _logger;

    public OperacaoHealthController(IHealthCheckService healthCheckService, IWebHostEnvironment environment, ILogger<OperacaoHealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("resumo")]
    public async Task<ActionResult<ApiResponse<object>>> Resumo(CancellationToken cancellationToken)
    {
        try
        {
            var db = await _healthCheckService.GetDatabaseAsync(cancellationToken).ConfigureAwait(false);
            var version = _healthCheckService.GetVersion();
            var data = new
            {
                api = "online",
                database = db.Status == HealthCheckStatus.Healthy ? "online" : "offline",
                migrations = db.Status == HealthCheckStatus.Healthy ? "ok" : "verificar",
                worker = "online",
                storage = "ok",
                environment = _environment.EnvironmentName,
                version = version.Version,
                serverTime = DateTimeOffset.UtcNow
            };
            return Ok(ApiResponse<object>.Ok(data, correlationId: CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resumo de health. CorrelationId={CorrelationId}", CorrelationId());
            return StatusCode(500, ApiResponse<object>.Fail("Não foi possível consultar o resumo de health.", CorrelationId()));
        }
    }

    private string CorrelationId() => HttpContext.Items[Middlewares.CorrelationIdMiddleware.HeaderName]?.ToString() ?? HttpContext.TraceIdentifier;
}
