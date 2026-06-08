using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/tributario")]
[RequireModule("tributario")]
public sealed class TributarioController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public TributarioController(IWebHostEnvironment environment) => _environment = environment;

    [HttpGet("dashboard")]
    public ActionResult<ApiResponse<object>> Dashboard() => Ok(ApiResponse<object>.Ok(new
    {
        contribuintesAtivos = 0,
        imoveisAtivos = 0,
        empresasAtivas = 0,
        lancamentosExercicio = 0,
        totalLancado = 0m,
        totalArrecadado = 0m,
        parcelasVencidas = 0,
        dividaAtiva = 0m,
        damsGerados = 0,
        carnesEmitidos = 0
    }));

    [HttpPost("dam-dev")]
    public ActionResult<ApiResponse<object>> GerarDamDev([FromBody] DevIntegrationRequest request) => DevOnly("DAM fake", request);

    [HttpPost("pix-dev")]
    public ActionResult<ApiResponse<object>> GerarPixDev([FromBody] DevIntegrationRequest request) => DevOnly("PIX dev", request);

    [HttpPost("pagamentos-dev")]
    public ActionResult<ApiResponse<object>> RegistrarPagamentoDev([FromBody] DevIntegrationRequest request) => DevOnly("pagamento dev", request);

    private ActionResult<ApiResponse<object>> DevOnly(string recurso, DevIntegrationRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Fail($"{recurso} disponível somente em Development. Integração real não configurada para este ambiente."));
        }

        return Ok(ApiResponse<object>.Ok(new { request.ParcelaId, request.Valor, ambiente = _environment.EnvironmentName }));
    }
}

public sealed record DevIntegrationRequest(long ParcelaId, decimal? Valor);
