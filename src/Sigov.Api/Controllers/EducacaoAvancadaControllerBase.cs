using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Api.Contracts;
using Sigov.Api.Filters;
using Sigov.Application.Abstractions;
using Sigov.Application.Educacao.Avancada;

namespace Sigov.Api.Controllers;

[Authorize]
[TypeFilter(typeof(EducacaoFalhasNomeadasFiltro))]
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
        catch (RegularizacaoLegadaExigeJustificativaException ex)
        {
            return Conflict(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ConflitoDeVersaoEducacaoException ex)
        {
            return Conflict(ApiResponse<bool>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (DistribuicaoEstornoObrigatorioException ex)
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
        catch (CardapioProdutoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CardapioProdutoDuplicadoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CardapioPeriodoExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (MerendaProdutoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (DistribuicaoCardapioInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CardapioForaDoEscopoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CardapioNaoAprovadoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (DataForaDoPeriodoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (DistribuicaoDiaExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstoqueInsuficienteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ProdutoForaDoMenuException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (BibliotecaExemplarInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (BibliotecaEmprestimoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (BibliotecaAlunoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (BibliotecaServidorInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EmprestimoTomadorInvalidoException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (PrazoEmprestimoInvalidoException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ExemplarForaDoEscopoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ContextoInstitucionalDivergenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ExemplarNaoDisponivelException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ExemplarIndisponivelException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EmprestimoJaDevolvidoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ReservaVigenteExisteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteRotaInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransportePontoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteAlunoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteRotaNaoVigenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransportePontoOrdemExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransportePontoRotaDivergenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteAlunoJaVinculadoARotaException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteRotaEscolaOuRegiaoInvalidoException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransportePontoOrdemInvalidaException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (TransporteTurnoVinculoDivergenteException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (FundebParametroCodigoExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (FundebReceitaDuplicadaException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CustoCentroInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CustoCentroNaoVigenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (CustoCentroCodigoExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (IndicadorCodigoExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (IndicadorInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (IndicadorNaoVigenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ValorIndicadorExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ProjecaoMatriculaExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (LoteEducacensoAnoExistenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (LoteEducacensoInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (LoteEducacensoNaoVigenteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstoqueOperacaoInvalidaException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstoqueNaoAbertoException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstoqueAberturaExisteException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (SaldoNegativoRejeitadoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ChaveIdempotenciaConflitoException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstornoMotivoObrigatorioException ex)
        {
            return StatusCode(400, ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstornoBaseInexistenteException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (EstornoBaseInvalidaException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (DistribuicaoEstornoIndisponivelException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (RegistroNaoEncontradoException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (RecursoNaoDisponivelException ex)
        {
            return NotFound(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ComandoTipadoObrigatorioException ex)
        {
            return Conflict(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<long>.Fail(ex.Message, GetCorrelationId()));
        }
    }
}
