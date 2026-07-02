using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[ApiController]
public sealed class ApiV1Controller : ControllerBase
{
    private static readonly string[] Recursos = { "tenants", "usuarios", "protocolos", "documentos", "contratos", "juridico", "tributario", "financeiro", "workflow", "notificacoes" };
    [HttpGet("/api/v1/health")] public IActionResult Health(CancellationToken ct) => Ok(new { status = "ok", version = "v1", correlationId = HttpContext.TraceIdentifier });
    [HttpGet("/api/v1/{resource}")] public IActionResult List(string resource, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!Recursos.Contains(resource, StringComparer.OrdinalIgnoreCase)) return NotFound(new { error = "Recurso não documentado na API pública SIGOV.", correlationId = HttpContext.TraceIdentifier });
        pageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(new { resource, page = Math.Max(page, 1), pageSize, items = Array.Empty<object>(), fallback = "Endpoint preparado para integração; dados reais dependem de autenticação, schema e contrato de API.", correlationId = HttpContext.TraceIdentifier });
    }
}
