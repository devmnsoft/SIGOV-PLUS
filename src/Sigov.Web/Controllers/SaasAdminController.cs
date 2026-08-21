using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Authorization;
using Sigov.Application.Saas.SuperAdmin;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("SaasAdmin")]
public sealed class SaasAdminController(ISuperAdminOperationalDashboardService dashboard, IAuthorizationEvaluator authorization,
    IAuthorizationAdminService authorizationAdmin) : Controller
{
    [HttpGet("Dashboard")]
    [HttpGet("Operacional")]
    public async Task<IActionResult> Dashboard(long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status, CancellationToken ct)
    {
        if (!await Allowed("visualizar", tenantId, ct)) return Forbid();
        var filter = Filter(tenantId, from, to, module, status);
        ViewBag.Filter = filter;
        ViewBag.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Não configurado";
        ViewBag.Version = typeof(SaasAdminController).Assembly.GetName().Version?.ToString() ?? "Não disponível";
        return View("Dashboard", await dashboard.GetAsync(filter, ct));
    }

    [HttpGet("Dashboard/Export")]
    public async Task<IActionResult> Export(string format, long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status, CancellationToken ct)
    {
        if (!await Allowed("exportar", tenantId, ct)) return Forbid();
        var data = await dashboard.GetAsync(Filter(tenantId, from, to, module, status), ct);
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            return File(JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true }), "application/json", "sigov-operacional.json");
        var csv = new StringBuilder("area;data;tenant;evento;status\n");
        foreach (var item in data.Authorizations)
            csv.Append(Csv("autorizacao")).Append(';').Append(Csv(item.AtUtc.ToString("O"))).Append(';').Append(Csv(item.TenantId?.ToString())).Append(';').Append(Csv($"{item.Resource}.{item.Action}")).Append(';').Append(Csv(item.Allowed ? "PERMITIR" : "NEGAR")).Append('\n');
        foreach (var item in data.Audits)
            csv.Append(Csv(item.Area)).Append(';').Append(Csv(item.AtUtc.ToString("O"))).Append(';').Append(Csv(item.TenantId?.ToString())).Append(';').Append(Csv(item.Event)).Append(';').Append(Csv(item.Result)).Append('\n');
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "sigov-operacional.csv");
    }

    [HttpGet("Tenants")] public IActionResult Tenants() => View();
    [HttpGet("TenantDetalhe")] public IActionResult TenantDetalhe() => View();
    [HttpGet("NovoTenant")] public IActionResult NovoTenant() => View();
    [HttpGet("Planos")] public IActionResult Planos() => View();
    [HttpGet("Modulos")] public IActionResult Modulos() => View();
    [HttpGet("Assinaturas")] public IActionResult Assinaturas() => View();
    [HttpGet("FeatureFlags")] public IActionResult FeatureFlags() => View();
    [HttpGet("Uso")] public IActionResult Uso() => View();
    [HttpGet("Autorizacao")]
    public async Task<IActionResult> Autorizacao(CancellationToken ct)
    {
        if (!await AllowedAdmin(ct)) return Forbid();
        return View();
    }

    [HttpGet("Autorizacao/Dados")]
    public async Task<IActionResult> AuthorizationData(string? search, long? tenantId, bool includeInactive, CancellationToken ct)
    {
        if (!await AllowedAdmin(ct)) return Forbid();
        return Json(await authorizationAdmin.ListAsync(new(search, tenantId, includeInactive), ct));
    }

    [HttpPost("Autorizacao/Catalogo/{kind}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthorizationCatalog(string kind, [FromBody] AuthorizationCatalogCommand command, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return Forbid();
        var result = await authorizationAdmin.SaveCatalogAsync(kind, command, identity.Value, HttpContext.TraceIdentifier, ct);
        return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity, result);
    }

    [HttpPost("Autorizacao/Vinculo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthorizationLink([FromBody] AuthorizationLinkCommand command, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return Forbid();
        var result = await authorizationAdmin.SaveLinkAsync(command, identity.Value, HttpContext.TraceIdentifier, ct);
        return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity, result);
    }

    [HttpPost("Autorizacao/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAuthorizationStatus([FromBody] AuthorizationStatusRequest request, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return Forbid();
        var result = await authorizationAdmin.ChangeStatusAsync(request.Kind, request.LeftId, request.RightId, request.Active, request.Delete,
            identity.Value, HttpContext.TraceIdentifier, ct);
        return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity, result);
    }

    private async Task<bool> AllowedAdmin(CancellationToken ct) => (await AdminIdentity(ct)) is not null;
    private async Task<long?> AdminIdentity(CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("usuario_id");
        if (!long.TryParse(raw, CultureInfo.InvariantCulture, out var userId)) return null;
        var decision = await authorization.EvaluateAsync(new(userId, "saas", "saas.superadmin.autorizacao", "administrar",
            CorrelationId: HttpContext.TraceIdentifier, Origem: "WEB_SUPERADMIN_AUTORIZACAO"), ct);
        return decision.Permitido ? userId : null;
    }
    private async Task<bool> Allowed(string action, long? tenantId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("usuario_id");
        if (!long.TryParse(raw, CultureInfo.InvariantCulture, out var userId)) return false;
        var decision = await authorization.EvaluateAsync(new(userId, "saas", "saas.superadmin.dashboard", action, tenantId,
            CorrelationId: HttpContext.TraceIdentifier, Origem: "WEB_SUPERADMIN_DASHBOARD"), ct);
        return decision.Permitido;
    }

    private static SuperAdminDashboardFilter Filter(long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-7);
        return new(tenantId, start > end ? end.AddDays(-7) : start, end, module, status);
    }

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && "=+-@".Contains(safe[0])) safe = "'" + safe;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }
}

public sealed record AuthorizationStatusRequest(string Kind, long LeftId, long? RightId, bool Active, bool Delete);
