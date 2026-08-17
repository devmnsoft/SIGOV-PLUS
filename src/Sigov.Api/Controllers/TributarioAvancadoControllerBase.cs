using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Tributario.TributarioAvancado;

namespace Sigov.Api.Controllers;

[Authorize]
public abstract class TributarioAvancadoControllerBase : ControllerBase
{
    protected readonly ICurrentTenant Tenant; protected readonly ICurrentUser User;
    protected TributarioAvancadoControllerBase(ICurrentTenant tenant, ICurrentUser user) { Tenant=tenant; User=user; }
    protected long TenantId() => Tenant.TenantId ?? throw new InvalidOperationException("Tenant autenticado é obrigatório.");
    protected TributarioAvancadoContext Contexto() => new(TenantId(),Tenant.EntidadeId,Tenant.ExercicioId,User.UsuarioId,HttpContext.TraceIdentifier);
    protected ActionResult<ApiResponse<T>> Resposta<T>(T value) => Ok(ApiResponse<T>.Ok(value,correlationId:HttpContext.TraceIdentifier));
}
