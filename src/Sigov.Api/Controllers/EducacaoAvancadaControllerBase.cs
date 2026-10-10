using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Educacao.Avancada;

namespace Sigov.Api.Controllers;

[Authorize]
public abstract class EducacaoAvancadaControllerBase : ControllerBase
{
    protected readonly ICurrentTenant Tenant;
    protected readonly ICurrentUser CurrentUser;

    protected EducacaoAvancadaControllerBase(ICurrentTenant tenant, ICurrentUser currentUser)
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

    protected EducacaoAvancadaContext Contexto() =>
        new(TenantId(), Tenant.EntidadeId, Tenant.ExercicioId, CurrentUser.UsuarioId, GetCorrelationId());

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T value) =>
        Ok(ApiResponse<T>.Ok(value, correlationId: GetCorrelationId()));

    /// <summary>
    /// Mapeia as falhas nomeadas da camada de repositório para respostas HTTP explícitas (regra 13):
    /// 409 TRANSICAO_INVALIDA / CONFLITO_VERSAO, 404 REGISTRO_NAO_ENCONTRADO / RECURSO_INDISPONIVEL.
    /// </summary>
    protected async Task<ActionResult<ApiResponse<bool>>> ExecutarStatusAsync(Func<Task<bool>> operacao)
    {
        try
        {
            return OkResponse(await operacao());
        }
        catch (TransicaoDeStatusInvalidaException ex)
        {
            return Conflict(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ConflitoDeVersaoEducacaoException ex)
        {
            return Conflict(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (RegistroNaoEncontradoException ex)
        {
            return NotFound(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (RecursoNaoDisponivelException ex)
        {
            return NotFound(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
    }
}
