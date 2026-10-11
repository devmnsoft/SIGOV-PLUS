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
        catch (ContextoInstitucionalDivergenteException ex)
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

    /// <summary>
    /// Mapeia falhas nomeadas da criação para respostas HTTP explícitas (Ciclo 1 #6): 404 ESCOLA_INEXISTENTE,
    /// 409 ESCOLA_FORA_DO_ESCOPO e 422 ANO_LETIVO_INEXISTENTE, além das rejeições de validação (400).
    /// </summary>
    protected async Task<ActionResult<ApiResponse<long>>> ExecutarCriarAsync(Func<Task<long>> operacao)
    {
        try
        {
            return OkResponse(await operacao());
        }
        catch (EscolaInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EscolaForaDoEscopoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (AnoLetivoInexistenteException ex)
        {
            return StatusCode(422, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
    }
}
