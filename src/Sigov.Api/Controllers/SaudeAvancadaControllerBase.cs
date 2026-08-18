using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saude.Avancada;

namespace Sigov.Api.Controllers;

[Authorize]
public abstract class SaudeAvancadaControllerBase : ControllerBase
{
    protected readonly ICurrentTenant Tenant;
    protected readonly ICurrentUser CurrentUser;

    protected SaudeAvancadaControllerBase(ICurrentTenant tenant, ICurrentUser currentUser)
    {
        Tenant = tenant;
        CurrentUser = currentUser;
    }

    protected long TenantId() => Tenant.TenantId ?? throw new InvalidOperationException("Tenant autenticado é obrigatório.");

    protected string GetAuditUserName() =>
        HttpContext?.User?.Identity?.Name
        ?? HttpContext?.User?.FindFirst("name")?.Value
        ?? HttpContext?.User?.FindFirst("sub")?.Value
        ?? "system";

    protected string GetCorrelationId() => HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    protected SaudeAvancadaContext Contexto() =>
        new(TenantId(), Tenant.EntidadeId, Tenant.ExercicioId, CurrentUser.UsuarioId, GetCorrelationId());

    protected ActionResult<ApiResponse<T>> Resposta<T>(T value) =>
        Ok(ApiResponse<T>.Ok(value, correlationId: GetCorrelationId()));
}
