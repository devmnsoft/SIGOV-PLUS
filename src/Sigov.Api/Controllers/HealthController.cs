using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<HealthController> _logger;

    public HealthController(NpgsqlConnectionFactory connectionFactory, ILogger<HealthController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<ApiResponse<object>> Get() => GetLive();

    [HttpGet("live")]
    public ActionResult<ApiResponse<object>> GetLive()
    {
        return Ok(ApiResponse<object>.Ok(new { status = "Healthy", service = "sigov API", version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev" }));
    }

    [HttpGet("ready")]
    public async Task<ActionResult<ApiResponse<object>>> GetReady(CancellationToken cancellationToken)
    {
        var db = await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false);
        return db.schemaExists
            ? Ok(ApiResponse<object>.Ok(new { status = "Ready", db.database, schema = "sigov" }))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Schema sigov indisponível."));
    }

    [HttpGet("storage")]
    public ActionResult<ApiResponse<object>> GetStorage() => Ok(ApiResponse<object>.Ok(new { status = "Healthy", provider = "configured" }));

    [HttpGet("outbox")]
    public ActionResult<ApiResponse<object>> GetOutbox() => Ok(ApiResponse<object>.Ok(new { status = "Healthy", queue = "sigov.fila_evento" }));

    [HttpGet("version")]
    public ActionResult<ApiResponse<object>> GetVersion() => Ok(ApiResponse<object>.Ok(new { application = "sigov", version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev" }));

    [HttpGet("db")]
    public async Task<ActionResult<ApiResponse<object>>> GetDb(CancellationToken cancellationToken)
    {
        try
        {
            var (database, schemaExists) = await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { status = "Healthy", database, schema = "sigov", schemaExists }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no health check DB sigov. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Banco sigov indisponível."));
        }
    }

    private async Task<(string database, bool schemaExists)> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("select current_database();", cancellationToken: cancellationToken)).ConfigureAwait(false) ?? "unknown";
        var schemaExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists (select 1 from information_schema.schemata where schema_name = 'sigov');", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (database, schemaExists);
    }
}
