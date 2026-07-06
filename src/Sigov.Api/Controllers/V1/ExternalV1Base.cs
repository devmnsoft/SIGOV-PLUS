using Microsoft.AspNetCore.Mvc;
using Sigov.Application.External;

namespace Sigov.Api.Controllers.V1;

[ApiController]
[Produces("application/json")]
public abstract class ExternalV1Base : ControllerBase
{
    protected string CorrelationId => HttpContext.TraceIdentifier;
    protected IActionResult OkEnvelope(object? data, string? message = null) => Ok(ExternalApiEnvelope.Ok(data, CorrelationId, message ?? "Operação realizada com sucesso."));
    protected IActionResult AcceptedFallback(string recurso) => OkEnvelope(new { items = Array.Empty<object>(), page = 1, pageSize = 20, total = 0, fallback = $"{recurso}: endpoint preparado com tenant obrigatório, paginação, LGPD e auditoria; persistência real depende do schema e credenciais válidas." });
    protected string? Tenant => Request.Headers["X-Tenant-Id"].FirstOrDefault();
}
