using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;
using Sigov.Application.Saas.Context;
using Sigov.Application.Saas.Modules;
using Sigov.Application.Saas.SuperAdmin;
using Sigov.Domain.Saas;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Web.Helpers;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("SaasAdmin")]
[Route("AdminMNSOFT")]
public sealed class SaasAdminController(ISuperAdminOperationalDashboardService dashboard, IAuthorizationEvaluator authorization,
    IAuthorizationAdminService authorizationAdmin, ISaasTenantAdministrationService tenants, IAuditTrailService audit,
    ITenantContextSwitchRepository switchRepo, NpgsqlConnectionFactory connectionFactory, ILogger<SaasAdminController> logger) : Controller
{
    [HttpGet("Dashboard")]
    [HttpGet("Operacional")]
    [HttpGet("")]
    public async Task<IActionResult> Dashboard(long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status, CancellationToken ct)
    {
        if (!await Allowed("visualizar", tenantId, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var filter = Filter(tenantId, from, to, module, status);
        ViewBag.Filter = filter;
        ViewBag.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Não configurado";
        ViewBag.Version = typeof(SaasAdminController).Assembly.GetName().Version?.ToString() ?? "Não disponível";
        return View("Dashboard", await dashboard.GetAsync(filter, ct));
    }

    [HttpGet("Dashboard/Export")]
    public async Task<IActionResult> Export(string format, long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status, CancellationToken ct)
    {
        if (!await Allowed("exportar", tenantId, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var data = await dashboard.GetAsync(Filter(tenantId, from, to, module, status) with { PageSize = null }, ct);
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
        if (!await Allowed("visualizar", null, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
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
        if (!await Allowed("visualizar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var detail = await tenants.GetAsync(id, ct).ConfigureAwait(false);
        if (detail is null) return NotFound();
        ViewBag.CanManageContracts = !IsLocalTenantAdmin() && await Allowed("administrar", id, ct);
        return View(detail);
    }

    [HttpPost("Tenants/{id:long}/Contratar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContratarModulo(long id, string moduleCode, DateOnly? effectiveFrom, DateOnly? effectiveUntil, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ContractAsync(new(id, moduleCode, "CONTRATADO", effectiveFrom, effectiveUntil, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Suspender")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspenderModulo(long id, string moduleCode, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.SuspendAsync(new(id, moduleCode, "SUSPENSO", null, null, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReativarModulo(long id, string moduleCode, DateOnly? effectiveFrom, DateOnly? effectiveUntil, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ReactivateAsync(new(id, moduleCode, "HABILITADO", effectiveFrom, effectiveUntil, justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Bloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BloquearTenant(long id, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ChangeTenantStatusAsync(new(id, "BLOQUEADO", justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Desbloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesbloquearTenant(long id, string justification, DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ChangeTenantStatusAsync(new(id, "ATIVO", justification ?? string.Empty, expectedUpdatedAt), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Usuarios/Criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CriarUsuario(long id, string name, string email, string? login, string? perfilCodigo, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.CreateUserAsync(new(id, name, email, login, "TENANT_USER", perfilCodigo), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        if (!result.Success && result.Motivo403.HasValue)
        {
            // RC-SAAS-AUT (A9): falha por limite transacional é 403 padronizado com motivo LIMITE_ATINGIDO.
            await audit.RegistrarAsync(id, identity.Value, "SAAS_USUARIO_CRIAR_LIMITE", "sigov.usuario", id.ToString(), null,
                new { motivo = SaasForbiddenMotivos.ToWire(result.Motivo403.Value), detalhe = result.Message },
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
            return ForbiddenResponse.Registrar(this, result.Motivo403.Value, result.Message);
        }
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Usuarios/{userId:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarUsuario(long id, long userId, string name, string email, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.UpdateUserAsync(new(id, userId, name, email), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Usuarios/{userId:long}/Bloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BloquearUsuario(long id, long userId, string justification, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ChangeUserStatusAsync(new(id, userId, true, true, justification ?? string.Empty), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Usuarios/{userId:long}/Desbloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesbloquearUsuario(long id, long userId, string justification, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ChangeUserStatusAsync(new(id, userId, true, false, justification ?? string.Empty), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/Usuarios/{userId:long}/Inativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InativarUsuario(long id, long userId, string justification, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var identity = CurrentUserId();
        if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await tenants.ChangeUserStatusAsync(new(id, userId, false, false, justification ?? string.Empty), identity.Value, HttpContext.TraceIdentifier, ct).ConfigureAwait(false);
        TempData[result.Success ? "SaasAdminSuccess" : "SaasAdminError"] = result.Message;
        return RedirectToAction(nameof(TenantDetalhe), new { id });
    }

    [HttpPost("Tenants/{id:long}/AlternarContexto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarContexto(long id, CancellationToken ct)
    {
        if (!await Allowed("administrar", id, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var userId = CurrentUserId();
        if (userId is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");

        // RC-SAAS-AUT (A4): troca de contexto auditada — atualiza a sessão em place e re-assina o
        // ticket com claims do tenant destino + contexto_auditado=true.
        try
        {
            var fromTenant = long.TryParse(User.FindFirstValue("tenant_id"), out var ft) ? ft : 0;
            var traceId = HttpContext.TraceIdentifier;
            var guid = Guid.TryParse(traceId, out var g) ? g : Guid.NewGuid();

            // 1. Iniciar auditoria da troca
            var logId = await switchRepo.StartSwitchAsync(new(
                userId.Value, id, null, $"ALTERAR_CONTEXTO para tenant {id}",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), guid), ct).ConfigureAwait(false);

            // 2. Atualizar identidade_sessao em place (mantém token_hash, auth_version; muda tenant_id)
            using var cn = connectionFactory.CreateConnection();
            await cn.OpenAsync(ct).ConfigureAwait(false);
            var updated = await cn.ExecuteAsync(new CommandDefinition(@"
                update sigov.identidade_sessao
                   set tenant_id = @To, exercicio_id = null, updated_at = now()
                 where usuario_id = @UserId and tenant_id = @From and encerrada_at is null;
            ", new { To = id, UserId = userId.Value, From = fromTenant }, cancellationToken: ct)).ConfigureAwait(false);
            if (updated == 0)
            {
                await switchRepo.FinishSwitchAsync(logId, userId.Value, ct).ConfigureAwait(false);
                TempData["SaasAdminError"] = "Não foi possível alternar o contexto. Sessão inválida.";
                return RedirectToAction(nameof(TenantDetalhe), new { id });
            }

            // 3. Re-SignIn com claims atualizadas
            var newClaims = User.Claims
                .Where(c => c.Type != "tenant_id" && c.Type != "contexto_auditado")
                .ToList();
            newClaims.Add(new Claim("tenant_id", id.ToString(CultureInfo.InvariantCulture)));
            newClaims.Add(new Claim("contexto_auditado", "true"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(newClaims, CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = true, AllowRefresh = true }).ConfigureAwait(false);

            // 4. Finalizar auditoria
            await switchRepo.FinishSwitchAsync(logId, userId.Value, ct).ConfigureAwait(false);

            TempData["SaasAdminSuccess"] = $"Contexto alternado para o tenant #{id}.";
            return Redirect("/Dashboard");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao alternar contexto para tenant {Id}. CorrelationId={CorrelationId}", id, HttpContext.TraceIdentifier);
            TempData["SaasAdminError"] = "Falha ao alternar o contexto. Tente novamente.";
            return RedirectToAction(nameof(TenantDetalhe), new { id });
        }
    }

    [HttpPost("VoltarContexto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoltarContexto(CancellationToken ct)
    {
        if (!await Allowed("administrar", null, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var userId = CurrentUserId();
        if (userId is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");

        var isAuditContext = User.FindFirst("contexto_auditado")?.Value == "true";
        if (!isAuditContext)
        {
            TempData["SaasAdminError"] = "Você não está em contexto de auditoria.";
            return RedirectToAction(nameof(Dashboard));
        }

        try
        {
            var currentTenant = long.TryParse(User.FindFirstValue("tenant_id"), out var ctVal) ? ctVal : 0;
            var traceId = HttpContext.TraceIdentifier;
            var guid = Guid.TryParse(traceId, out var g) ? g : Guid.NewGuid();

            var logId = await switchRepo.StartSwitchAsync(new(
                userId.Value, null, null, "RETORNO_CONTEXTO para tenant original",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), guid), ct).ConfigureAwait(false);

            // Reverter para o tenant original (sigov.usuario.tenant_id)
            using var cn = connectionFactory.CreateConnection();
            await cn.OpenAsync(ct).ConfigureAwait(false);
            var originalTenant = await cn.ExecuteScalarAsync<long?>(new CommandDefinition(
                "select tenant_id from sigov.usuario where id=@UserId limit 1;",
                new { UserId = userId.Value }, cancellationToken: ct)).ConfigureAwait(false);
            if (originalTenant is null || originalTenant.Value <= 0)
            {
                await switchRepo.FinishSwitchAsync(logId, userId.Value, ct).ConfigureAwait(false);
                TempData["SaasAdminError"] = "Tenant original não encontrado.";
                return RedirectToAction(nameof(Dashboard));
            }

            var updated = await cn.ExecuteAsync(new CommandDefinition(@"
                update sigov.identidade_sessao
                   set tenant_id = @To, exercicio_id = null, updated_at = now()
                 where usuario_id = @UserId and tenant_id = @From and encerrada_at is null;
            ", new { To = originalTenant.Value, UserId = userId.Value, From = currentTenant }, cancellationToken: ct)).ConfigureAwait(false);
            if (updated == 0)
            {
                await switchRepo.FinishSwitchAsync(logId, userId.Value, ct).ConfigureAwait(false);
                TempData["SaasAdminError"] = "Não foi possível voltar ao contexto original.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Re-SignIn sem contexto_auditado
            var newClaims = User.Claims
                .Where(c => c.Type != "tenant_id" && c.Type != "contexto_auditado")
                .ToList();
            newClaims.Add(new Claim("tenant_id", originalTenant.Value.ToString(CultureInfo.InvariantCulture)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(newClaims, CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = true, AllowRefresh = true }).ConfigureAwait(false);

            await switchRepo.FinishSwitchAsync(logId, userId.Value, ct).ConfigureAwait(false);
            TempData["SaasAdminSuccess"] = "Contexto original restaurado.";
            return Redirect("/Dashboard");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao voltar do contexto. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            TempData["SaasAdminError"] = "Falha ao voltar ao contexto original.";
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [HttpGet("NovoTenant"), HttpGet("Clientes/Create")]
    public async Task<IActionResult> NovoTenant(CancellationToken ct) => await Allowed("administrar", null, ct) ? Redirect("/Saas/Tenants") : Forbid();

    [HttpGet("Planos")]
    public async Task<IActionResult> Planos(CancellationToken ct) => await Allowed("administrar", null, ct) ? Redirect("/Saas/Planos") : Forbid();

    [HttpGet("Modulos"), HttpGet("Funcionalidades"), HttpGet("Bloqueios")]
    public async Task<IActionResult> Modulos(CancellationToken ct) => await Allowed("administrar", null, ct) ? Redirect("/Saas/Modulos") : Forbid();

    [HttpGet("Assinaturas"), HttpGet("Cobrancas")]
    public async Task<IActionResult> Assinaturas(CancellationToken ct) => await Allowed("administrar", null, ct) ? Redirect("/Saas/Assinaturas") : Forbid();

    [HttpGet("FeatureFlags")]
    public async Task<IActionResult> FeatureFlags(CancellationToken ct) => await Allowed("administrar", null, ct) ? Redirect("/Saas/Modulos") : Forbid();

    [HttpGet("Uso")]
    public async Task<IActionResult> Uso(long? tenantId, DateTimeOffset? from, DateTimeOffset? to, string? module, string? status, int page = 1, CancellationToken ct = default)
    {
        if (!await Allowed("visualizar", tenantId, ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        if (IsLocalTenantAdmin() && long.TryParse(User.FindFirstValue("tenant_id"), out var ownTenant))
            tenantId = ownTenant;

        var filter = Filter(tenantId, from, to, module, status) with { PageNumber = Math.Max(1, page), PageSize = 25 };
        if (from.HasValue && to.HasValue && from > to)
            ViewBag.FilterError = "A data inicial deve ser anterior ou igual à data final.";
        else if (filter.ToUtc - filter.FromUtc > TimeSpan.FromDays(366))
            ViewBag.FilterError = "O período consultado não pode ultrapassar 366 dias.";
        var model = await dashboard.GetAsync(filter, ct).ConfigureAwait(false);
        var lastPage = Math.Max(1, (int)Math.Ceiling(model.AuditTotal / (double)filter.PageSize.Value));
        if (filter.PageNumber > lastPage)
        {
            filter = filter with { PageNumber = lastPage };
            model = await dashboard.GetAsync(filter, ct).ConfigureAwait(false);
        }
        ViewBag.Filter = filter;
        return View(model);
    }

    [HttpGet("Relatorios"), HttpGet("Auditoria"), HttpGet("Sessoes"), HttpGet("Usuarios"), HttpGet("PerfisGlobais")]
    public async Task<IActionResult> Operacao(CancellationToken ct) => await Allowed("visualizar", null, ct) ? View() : Forbid();
    [HttpGet("Autorizacao")]
    public async Task<IActionResult> Autorizacao(CancellationToken ct)
    {
        if (!await AllowedAdmin(ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        return View();
    }

    [HttpGet("Autorizacao/Dados")]
    public async Task<IActionResult> AuthorizationData(string? search, long? tenantId, bool includeInactive, CancellationToken ct)
    {
        if (!await AllowedAdmin(ct)) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        return Json(await authorizationAdmin.ListAsync(new(search, tenantId, includeInactive), ct));
    }

    [HttpPost("Autorizacao/Catalogo/{kind}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthorizationCatalog(string kind, [FromBody] AuthorizationCatalogCommand command, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await authorizationAdmin.SaveCatalogAsync(kind, command, identity.Value, HttpContext.TraceIdentifier, ct);
        return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity, result);
    }

    [HttpPost("Autorizacao/Vinculo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAuthorizationLink([FromBody] AuthorizationLinkCommand command, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
        var result = await authorizationAdmin.SaveLinkAsync(command, identity.Value, HttpContext.TraceIdentifier, ct);
        return StatusCode(result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity, result);
    }

    [HttpPost("Autorizacao/Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAuthorizationStatus([FromBody] AuthorizationStatusRequest request, CancellationToken ct)
    {
        var identity = await AdminIdentity(ct); if (identity is null) return ForbiddenResponse.Registrar(this, SaasForbiddenMotivo.SemPermissao, "Sem permissão para executar esta operação.");
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
        return new(tenantId, start, end, module, status);
    }

    private static string Csv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Length > 0 && "=+-@".Contains(safe[0])) safe = "'" + safe;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }
}

public sealed record AuthorizationStatusRequest(string Kind, long LeftId, long? RightId, bool Active, bool Delete);
