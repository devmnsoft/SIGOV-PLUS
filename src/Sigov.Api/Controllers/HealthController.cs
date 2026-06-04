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
    public ActionResult<ApiResponse<object>> Get()
    {
        return Ok(ApiResponse<object>.Ok(new { status = "Healthy", service = "sigov API" }));
    }

    [HttpGet("db")]
    public async Task<ActionResult<ApiResponse<object>>> GetDb(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("select current_database();", cancellationToken: cancellationToken)).ConfigureAwait(false);
            var schemaExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists (select 1 from information_schema.schemata where schema_name = 'sigov');", cancellationToken: cancellationToken)).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { status = "Healthy", database, schema = "sigov", schemaExists }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no health check DB sigov. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Banco sigov indisponível."));
        }
    }
}
