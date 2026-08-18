using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saneamento.Avancado;

namespace Sigov.Api.Controllers;

[Authorize]
public abstract class SaneamentoAvancadoControllerBase : ControllerBase
{
    private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user;
    protected SaneamentoAvancadoControllerBase(ICurrentTenant tenant, ICurrentUser user) { _tenant = tenant; _user = user; }
    protected long TenantId() => _tenant.TenantId ?? throw new InvalidOperationException("Tenant autenticado é obrigatório.");
    protected SaneamentoAvancadoContext Contexto() => new(TenantId(), _tenant.EntidadeId, _tenant.ExercicioId, _user.UsuarioId, HttpContext.TraceIdentifier);
    protected ActionResult<ApiResponse<T>> Resposta<T>(T value) => Ok(ApiResponse<T>.Ok(value, correlationId: HttpContext.TraceIdentifier));
}
