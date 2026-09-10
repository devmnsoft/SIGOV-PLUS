using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;
using Sigov.Application.Saas.SuperAdmin;
using Sigov.Domain.Saas;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("SaasAdmin")]
[Route("AdminMNSOFT")]
public sealed class SaasAdminController(ISuperAdminOperationalDashboardService dashboard, IAuthorizationEvaluator authorization,
    IAuthorizationAdminService authorizationAdmin, ISaasTenantAdministrationService tenants) : Controller
{
    [HttpGet("Dashboard")]
    [HttpGet("Operacional")]
    [HttpGet("")]
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

    [HttpGet("Tenants"), HttpGet("Clientes")]
    public async Task<IActionResult> Tenants(string? search, string? status, string? esfera, int page = 1, CancellationToken ct = default)
    {
        if (!await Allowed("visualizar", null, ct)) return Forbid();
        if (IsLocalTenantAdmin() && long.TryParse(User.FindFirstValue("tenant_id"), out var ownTenant))
            return RedirectToAction(nameof(TenantDetalhe), new { id = ownTenant });
        var model = await tenants.ListAsync(new(search, status, esfera, page, 20), ct).ConfigureAwait(false);
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Esfera = esfera;
        return View(model);
    }

    [HttpGet("TenantDetalhe"), HttpGet("Clientes/Details"), HttpGet("Clientes/Edit")]
    public async Task<IActionResult> TenantDetalhe(long id, CancellationToken ct)
    {
        if (!await Allowed("visualizar", id, ct)) return Forbid();
        var detail = await tenants.GetAsync(id, ct).ConfigureAwait(false);
        if (detail is null) return NotFound();
        ViewBag.CanManageContracts = !IsLocalTenantAdmin() && await Allowed("administrar", id, ct);
        return View(detail);
    }

    [HttpPost("Tenants/{id:long}/Contratar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContratarModulo(long id, string moduleCode, DateOnly? effectiveFrom, DateOnly? effectiveUntil, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return Forbid();
        var identity = CurrentUserId();
        if (identity is null) return Forbid();
        var result = await tenants.ContractAsync(new(id, moduleCode, "CONTRATADO", effectiveFrom, effectiveUntil, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Suspender")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspenderModulo(long id, string moduleCode, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return Forbid();
        var identity = CurrentUserId();
        if (identity is null) return Forbid();
        var result = await tenants.SuspendAsync(new(id, moduleCode, "SUSPENSO", null, null, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReativarModulo(long id, string moduleCode, DateOnly? effectiveFrom, DateOnly? effectiveUntil, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return Forbid();
        var identity = CurrentUserId();
        if (identity is null) return Forbid();
        var result = await tenants.ReactivateAsync(new(id, moduleCode, "HABILITADO", effectiveFrom, effectiveUntil, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpGet("NovoTenant"), HttpGet("Clientes/Create")]
    public async Task<IActionResult> NovoTenant(CancellationToken ct) => await Allowed("administrar", null, ct) ? View() : Forbid();

    [HttpGet("Planos")]
    public async Task<IActionResult> Planos(CancellationToken ct) => await Allowed("administrar", null, ct) ? View() : Forbid();

    [HttpGet("Modulos"), HttpGet("Funcionalidades"), HttpGet("Bloqueios")]
    public async Task<IActionResult> Modulos(CancellationToken ct) => await Allowed("administrar", null, ct) ? View() : Forbid();

    [HttpGet("Assinaturas"), HttpGet("Cobrancas")]
    public async Task<IActionResult> Assinaturas(CancellationToken ct) => await Allowed("administrar", null, ct) ? View() : Forbid();

    [HttpGet("FeatureFlags")]
    public async Task<IActionResult> FeatureFlags(CancellationToken ct) => await Allowed("administrar", null, ct) ? View() : Forbid();

    [HttpGet("Uso"), HttpGet("Relatorios"), HttpGet("Auditoria"), HttpGet("Sessoes"), HttpGet("Usuarios"), HttpGet("PerfisGlobais")]
    public async Task<IActionResult> Uso(CancellationToken ct) => await Allowed("visualizar", null, ct) ? View() : Forbid();
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
            TenantId: null, CorrelationId: HttpContext.TraceIdentifier, Origem: "WEB_SUPERADMIN_AUTORIZACAO"), ct);
        return decision.Permitido ? userId : null;
    }
    private long? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("usuario_id");
        return long.TryParse(raw, CultureInfo.InvariantCulture, out var userId) ? userId : null;
    }

    private bool IsLocalTenantAdmin()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value);
        return roles.Any(role => string.Equals(role, PerfilNivelCodigos.AdministradorTenant, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "ADMIN_TENANT", StringComparison.OrdinalIgnoreCase))
            && !roles.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);
    }

    private async Task<bool> Allowed(string action, long? tenantId, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return false;
        if (IsLocalTenantAdmin())
        {
            var ownTenant = long.TryParse(User.FindFirstValue("tenant_id"), out var currentTenant) ? currentTenant : (long?)null;
            if (string.Equals(action, "administrar", StringComparison.OrdinalIgnoreCase))
                return false;
            return ownTenant.HasValue && (!tenantId.HasValue || tenantId == ownTenant);
        }
        var decision = await authorization.EvaluateAsync(new(userId.Value, "saas", "saas.superadmin.dashboard", action, tenantId,
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
