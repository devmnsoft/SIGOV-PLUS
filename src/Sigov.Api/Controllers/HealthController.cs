using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Health;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(IHealthCheckService healthCheckService) => _healthCheckService = healthCheckService;

    [HttpGet]
    public ActionResult<ApiResponse<object>> Get() => Ok(ApiResponse<object>.Ok(_healthCheckService.GetLive()));

    [HttpGet("live")]
    public ActionResult<ApiResponse<object>> GetLive() => Ok(ApiResponse<object>.Ok(_healthCheckService.GetLive()));

    [HttpGet("ready")]
    public async Task<ActionResult<ApiResponse<object>>> GetReady(CancellationToken cancellationToken)
    {
        var summary = await _healthCheckService.GetReadyAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(summary.Status, "Ready", StringComparison.Ordinal)
            ? Ok(ApiResponse<object>.Ok(summary))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Schema sigov indisponível."));
    }

    [HttpGet("storage")]
    public ActionResult<ApiResponse<object>> GetStorage() => Ok(ApiResponse<object>.Ok(_healthCheckService.GetStorage()));

    [HttpGet("outbox")]
    public async Task<ActionResult<ApiResponse<object>>> GetOutbox(CancellationToken cancellationToken)
    {
        var result = await _healthCheckService.GetOutboxAsync(cancellationToken).ConfigureAwait(false);
        return result.Status == HealthCheckStatus.Unhealthy
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Outbox sigov indisponível."))
            : Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("version")]
    public ActionResult<ApiResponse<object>> GetVersion() => Ok(ApiResponse<object>.Ok(_healthCheckService.GetVersion()));

    [HttpGet("db")]
    [HttpGet("database")]
    public async Task<ActionResult<ApiResponse<object>>> GetDb(CancellationToken cancellationToken)
    {
        var result = await _healthCheckService.GetDatabaseAsync(cancellationToken).ConfigureAwait(false);
        return result.Status == HealthCheckStatus.Healthy
            ? Ok(ApiResponse<object>.Ok(result))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Banco sigov indisponível."));
    }

    [HttpGet("runtime")]
    public async Task<ActionResult<object>> GetRuntime(CancellationToken cancellationToken)
    {
        var database = await _healthCheckService.GetDatabaseAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            status = database.Status.ToString(),
            environment = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName,
            runtime = new
            {
                migrationMode = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Sigov:Database:MigrationMode"] ?? "Disabled"
            },
            database
        });
    }

    [HttpGet("workers")]
    public ActionResult<object> GetWorkers() => Ok(new
    {
        status = "Healthy",
        workerEnabled = bool.TryParse(Environment.GetEnvironmentVariable("SIGOV_RUN_WORKER"), out var enabled) && enabled,
        ocrEnabled = bool.TryParse(Environment.GetEnvironmentVariable("OcrWorker__Enabled"), out var ocr) && ocr,
        previewEnabled = bool.TryParse(Environment.GetEnvironmentVariable("PreviewWorker__Enabled"), out var preview) && preview
    });
}
