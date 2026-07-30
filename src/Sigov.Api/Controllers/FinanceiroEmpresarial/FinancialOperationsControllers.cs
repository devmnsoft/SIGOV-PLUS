using System.Data;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.FinanceiroEmpresarial;

namespace Sigov.Api.Controllers.FinanceiroEmpresarial;

[ApiController]
[RequireModule("financeiro_empresarial")]
public abstract class FinancialControllerBase(IFinanceiroEmpresarialRepository repository, ICurrentTenant tenant, ICurrentUser user) : ControllerBase
{
    protected IFinanceiroEmpresarialRepository Repository { get; } = repository;
    protected long TenantId => tenant.TenantId ?? throw new UnauthorizedAccessException("Tenant autenticado obrigatório.");
    protected long? UserId => user.UsuarioId;
    protected Guid CorrelationId => Guid.TryParse(HttpContext.TraceIdentifier, out var value) ? value : Guid.NewGuid();
    protected ActionResult<ApiResponse<T>> Error<T>(Exception ex)
    {
        var (status, message) = ex switch { KeyNotFoundException => (404, ex.Message), DBConcurrencyException => (409, ex.Message), InvalidOperationException => (422, ex.Message), ArgumentException => (400, ex.Message), UnauthorizedAccessException => (401, ex.Message), _ => (500, "Falha interna na operação financeira.") };
        return StatusCode(status, ApiResponse<T>.Fail(message, CorrelationId.ToString()));
    }
}

[Route("api/financeiro-empresarial/contas-receber")]
public sealed class AccountsReceivableController(IFinanceiroEmpresarialRepository repository, ICurrentTenant tenant, ICurrentUser user) : FinancialControllerBase(repository, tenant, user)
{
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ContaFinanceiraResumoDto>>>> Listar([FromQuery] ContaFinanceiraFiltro filtro, CancellationToken ct) { try { return Ok(ApiResponse<PagedResult<ContaFinanceiraResumoDto>>.Ok(await Repository.ListarReceberAsync(TenantId, filtro, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<PagedResult<ContaFinanceiraResumoDto>>(ex); } }
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar(CriarContaReceberRequest request, CancellationToken ct) { try { var id = await Repository.CriarReceberAsync(TenantId, request, UserId, CorrelationId, ct); return CreatedAtAction(nameof(Listar), ApiResponse<long>.Ok(id, "CONTA_RECEBER_CRIADA", CorrelationId.ToString())); } catch (Exception ex) { return Error<long>(ex); } }
    [HttpPost("{id:long}/baixar")] public async Task<ActionResult<ApiResponse<BaixaFinanceiraDto>>> Baixar(long id, BaixarContaRequest request, CancellationToken ct) { try { return Ok(ApiResponse<BaixaFinanceiraDto>.Ok(await Repository.BaixarReceberAsync(TenantId, id, request, UserId, CorrelationId, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<BaixaFinanceiraDto>(ex); } }
    [HttpPost("{id:long}/estornar")] public async Task<ActionResult<ApiResponse<BaixaFinanceiraDto>>> Estornar(long id, EstornarBaixaRequest request, CancellationToken ct) { try { return Ok(ApiResponse<BaixaFinanceiraDto>.Ok(await Repository.EstornarReceberAsync(TenantId, id, request, UserId, CorrelationId, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<BaixaFinanceiraDto>(ex); } }
}

[Route("api/financeiro-empresarial/contas-pagar")]
public sealed class AccountsPayableController(IFinanceiroEmpresarialRepository repository, ICurrentTenant tenant, ICurrentUser user) : FinancialControllerBase(repository, tenant, user)
{
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ContaFinanceiraResumoDto>>>> Listar([FromQuery] ContaFinanceiraFiltro filtro, CancellationToken ct) { try { return Ok(ApiResponse<PagedResult<ContaFinanceiraResumoDto>>.Ok(await Repository.ListarPagarAsync(TenantId, filtro, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<PagedResult<ContaFinanceiraResumoDto>>(ex); } }
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar(CriarContaPagarRequest request, CancellationToken ct) { try { var id = await Repository.CriarPagarAsync(TenantId, request, UserId, CorrelationId, ct); return CreatedAtAction(nameof(Listar), ApiResponse<long>.Ok(id, "CONTA_PAGAR_CRIADA", CorrelationId.ToString())); } catch (Exception ex) { return Error<long>(ex); } }
    [HttpPost("{id:long}/pagar")] public async Task<ActionResult<ApiResponse<BaixaFinanceiraDto>>> Pagar(long id, BaixarContaRequest request, CancellationToken ct) { try { return Ok(ApiResponse<BaixaFinanceiraDto>.Ok(await Repository.BaixarPagarAsync(TenantId, id, request, UserId, CorrelationId, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<BaixaFinanceiraDto>(ex); } }
    [HttpPost("{id:long}/estornar")] public async Task<ActionResult<ApiResponse<BaixaFinanceiraDto>>> Estornar(long id, EstornarBaixaRequest request, CancellationToken ct) { try { return Ok(ApiResponse<BaixaFinanceiraDto>.Ok(await Repository.EstornarPagarAsync(TenantId, id, request, UserId, CorrelationId, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<BaixaFinanceiraDto>(ex); } }
}

[Route("api/financeiro-empresarial")]
public sealed class FinancialMovementsController(IFinanceiroEmpresarialRepository repository, ICurrentTenant tenant, ICurrentUser user) : FinancialControllerBase(repository, tenant, user)
{
    [HttpPost("transferencias")] public async Task<ActionResult<ApiResponse<TransferenciaFinanceiraDto>>> Transferir(TransferirValoresRequest request, CancellationToken ct) { try { return Ok(ApiResponse<TransferenciaFinanceiraDto>.Ok(await Repository.TransferirAsync(TenantId, request, UserId, CorrelationId, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<TransferenciaFinanceiraDto>(ex); } }
}

[Route("api/financeiro-empresarial")]
public sealed class FinancialDashboardController(IFinanceiroEmpresarialRepository repository, ICurrentTenant tenant, ICurrentUser user) : FinancialControllerBase(repository, tenant, user)
{
    [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<FinanceiroDashboardDto>>> Dashboard([FromQuery] DateOnly? inicio, [FromQuery] DateOnly? fim, CancellationToken ct) { try { var hoje = DateOnly.FromDateTime(DateTime.UtcNow); return Ok(ApiResponse<FinanceiroDashboardDto>.Ok(await Repository.DashboardAsync(TenantId, inicio ?? new DateOnly(hoje.Year, hoje.Month, 1), fim ?? hoje, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<FinanceiroDashboardDto>(ex); } }
    [HttpGet("fluxo-caixa")] public async Task<ActionResult<ApiResponse<IReadOnlyList<FluxoCaixaDto>>>> Fluxo([FromQuery] DateOnly inicio, [FromQuery] DateOnly fim, CancellationToken ct) { try { return Ok(ApiResponse<IReadOnlyList<FluxoCaixaDto>>.Ok(await Repository.FluxoCaixaAsync(TenantId, inicio, fim, ct), correlationId: CorrelationId.ToString())); } catch (Exception ex) { return Error<IReadOnlyList<FluxoCaixaDto>>(ex); } }
}
