using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Financeiro;

namespace Sigov.Api.Controllers;

[ApiController]
[RequireModule("financeiro_empresarial")]
public abstract class FinanceiroApiControllerBase : ProcessosControllerBase { }

[Route("api/financeiro/plano-contas")]
public sealed class PlanoContasController : FinanceiroApiControllerBase
{ private readonly IPlanoContasService _s; public PlanoContasController(IPlanoContasService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<PlanoContasResponse>>>> Listar([FromQuery]PlanoContasFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<PlanoContasResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterAsync(id,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]PlanoContasCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]PlanoContasUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/fontes-recurso")]
public sealed class FontesRecursoController : FinanceiroApiControllerBase
{ private readonly IFonteRecursoService _s; public FontesRecursoController(IFonteRecursoService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<FonteRecursoResponse>>>> Listar([FromQuery]FonteRecursoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<FonteRecursoResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterAsync(id,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]FonteRecursoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]FonteRecursoUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/programas")]
public sealed class ProgramasController : FinanceiroApiControllerBase
{ private readonly IProgramaService _s; public ProgramasController(IProgramaService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ProgramaResponse>>>> Listar([FromQuery]ProgramaFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]ProgramaCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]ProgramaUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/acoes")]
public sealed class AcoesController : FinanceiroApiControllerBase
{ private readonly IAcaoService _s; public AcoesController(IAcaoService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<AcaoResponse>>>> Listar([FromQuery]AcaoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]AcaoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]AcaoUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/naturezas-despesa")]
public sealed class NaturezasDespesaController : FinanceiroApiControllerBase
{ private readonly INaturezaDespesaService _s; public NaturezasDespesaController(INaturezaDespesaService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<NaturezaDespesaResponse>>>> Listar([FromQuery]NaturezaDespesaFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]NaturezaDespesaCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]NaturezaDespesaUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/naturezas-receita")]
public sealed class NaturezasReceitaController : FinanceiroApiControllerBase
{ private readonly INaturezaReceitaService _s; public NaturezasReceitaController(INaturezaReceitaService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<NaturezaReceitaResponse>>>> Listar([FromQuery]NaturezaReceitaFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]NaturezaReceitaCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]NaturezaReceitaUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _s.ExcluirAsync(id,ct).ConfigureAwait(false)); }
[Route("api/financeiro/orcamento")]
public sealed class OrcamentoController : FinanceiroApiControllerBase
{ private readonly IOrcamentoService _s; public OrcamentoController(IOrcamentoService s)=>_s=s; [HttpGet("despesas")] public async Task<ActionResult<ApiResponse<PagedResult<OrcamentoDespesaResponse>>>> ListarDespesas([FromQuery]OrcamentoDespesaFiltro f,CancellationToken ct)=>FromResult(await _s.ListarDespesasAsync(f,ct).ConfigureAwait(false)); [HttpGet("despesas/{id:long}")] public async Task<ActionResult<ApiResponse<OrcamentoDespesaResponse>>> ObterDespesa(long id,CancellationToken ct)=>FromResult(await _s.ObterDespesaAsync(id,ct).ConfigureAwait(false)); [HttpPost("despesas")] public async Task<ActionResult<ApiResponse<long>>> CriarDespesa([FromBody]OrcamentoDespesaCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarDespesaAsync(r,ct).ConfigureAwait(false)); [HttpPut("despesas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarDespesa(long id,[FromBody]OrcamentoDespesaUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarDespesaAsync(id,r,ct).ConfigureAwait(false)); [HttpPost("despesas/{id:long}/movimentar")] public async Task<ActionResult<ApiResponse<object>>> Movimentar(long id,[FromBody]MovimentacaoOrcamentariaRequest r,CancellationToken ct)=>FromResult(await _s.MovimentarDespesaAsync(id,r,ct).ConfigureAwait(false)); [HttpGet("receitas")] public async Task<ActionResult<ApiResponse<PagedResult<OrcamentoReceitaResponse>>>> ListarReceitas([FromQuery]OrcamentoReceitaFiltro f,CancellationToken ct)=>FromResult(await _s.ListarReceitasAsync(f,ct).ConfigureAwait(false)); [HttpGet("receitas/{id:long}")] public async Task<ActionResult<ApiResponse<OrcamentoReceitaResponse>>> ObterReceita(long id,CancellationToken ct)=>FromResult(await _s.ObterReceitaAsync(id,ct).ConfigureAwait(false)); [HttpPost("receitas")] public async Task<ActionResult<ApiResponse<long>>> CriarReceita([FromBody]OrcamentoReceitaCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarReceitaAsync(r,ct).ConfigureAwait(false)); [HttpPut("receitas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarReceita(long id,[FromBody]OrcamentoReceitaUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarReceitaAsync(id,r,ct).ConfigureAwait(false)); }
[Route("api/financeiro/empenhos")]
public sealed class EmpenhosController : FinanceiroApiControllerBase
{ private readonly IEmpenhoService _s; public EmpenhosController(IEmpenhoService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<EmpenhoResumoResponse>>>> Listar([FromQuery]EmpenhoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<EmpenhoDetalheResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterAsync(id,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]EmpenhoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(r,ct).ConfigureAwait(false)); [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody]EmpenhoUpdateRequest r,CancellationToken ct)=>FromResult(await _s.AtualizarAsync(id,r,ct).ConfigureAwait(false)); [HttpPost("{id:long}/anular")] public async Task<ActionResult<ApiResponse<object>>> Anular(long id,[FromBody]AnularEmpenhoRequest r,CancellationToken ct)=>FromResult(await _s.AnularAsync(id,r,ct).ConfigureAwait(false)); }
[Route("api/financeiro/liquidacoes")]
public sealed class LiquidacoesController : FinanceiroApiControllerBase
{ private readonly ILiquidacaoService _s; public LiquidacoesController(ILiquidacaoService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<LiquidacaoResponse>>>> Listar([FromQuery]LiquidacaoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<LiquidacaoResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterAsync(id,ct).ConfigureAwait(false)); [HttpPost("/api/financeiro/empenhos/{id:long}/liquidacoes")] public async Task<ActionResult<ApiResponse<long>>> Criar(long id,[FromBody]LiquidacaoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(id,r,ct).ConfigureAwait(false)); [HttpPost("{id:long}/anular")] public async Task<ActionResult<ApiResponse<object>>> Anular(long id,[FromBody]AnularLiquidacaoRequest r,CancellationToken ct)=>FromResult(await _s.AnularAsync(id,r,ct).ConfigureAwait(false)); }
[Route("api/financeiro/pagamentos")]
public sealed class PagamentosController : FinanceiroApiControllerBase
{ private readonly IPagamentoService _s; public PagamentosController(IPagamentoService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<PagamentoResponse>>>> Listar([FromQuery]PagamentoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarAsync(f,ct).ConfigureAwait(false)); [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<PagamentoResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterAsync(id,ct).ConfigureAwait(false)); [HttpPost("/api/financeiro/liquidacoes/{id:long}/pagamentos")] public async Task<ActionResult<ApiResponse<long>>> Criar(long id,[FromBody]PagamentoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarAsync(id,r,ct).ConfigureAwait(false)); [HttpPost("{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id,[FromBody]CancelarPagamentoRequest r,CancellationToken ct)=>FromResult(await _s.CancelarAsync(id,r,ct).ConfigureAwait(false)); }
[Route("api/financeiro/receitas")]
public sealed class ReceitasController : FinanceiroApiControllerBase
{ private readonly IReceitaService _s; public ReceitasController(IReceitaService s)=>_s=s; [HttpGet("lancamentos")] public async Task<ActionResult<ApiResponse<PagedResult<ReceitaLancamentoResponse>>>> Listar([FromQuery]ReceitaLancamentoFiltro f,CancellationToken ct)=>FromResult(await _s.ListarLancamentosAsync(f,ct).ConfigureAwait(false)); [HttpGet("lancamentos/{id:long}")] public async Task<ActionResult<ApiResponse<ReceitaLancamentoResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _s.ObterLancamentoAsync(id,ct).ConfigureAwait(false)); [HttpPost("lancamentos")] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody]ReceitaLancamentoCreateRequest r,CancellationToken ct)=>FromResult(await _s.CriarLancamentoAsync(r,ct).ConfigureAwait(false)); [HttpPost("lancamentos/{id:long}/arrecadacoes")] public async Task<ActionResult<ApiResponse<long>>> Arrecadar(long id,[FromBody]ReceitaArrecadacaoCreateRequest r,CancellationToken ct)=>FromResult(await _s.ArrecadarAsync(id,r,ct).ConfigureAwait(false)); }
[Route("api/financeiro/dashboard")]
public sealed class FinanceiroDashboardController : FinanceiroApiControllerBase { private readonly IFinanceiroDashboardService _s; public FinanceiroDashboardController(IFinanceiroDashboardService s)=>_s=s; [HttpGet] public async Task<ActionResult<ApiResponse<FinanceiroDashboardResponse>>> Obter(CancellationToken ct)=>FromResult(await _s.ObterAsync(ct).ConfigureAwait(false)); }
[Route("api/financeiro/export")]
public sealed class FinanceiroExportacaoController : FinanceiroApiControllerBase { private readonly IFinanceiroExportacaoService _s; public FinanceiroExportacaoController(IFinanceiroExportacaoService s)=>_s=s; [HttpGet("{recurso}.{formato}")] public async Task<IActionResult> Exportar(string recurso,string formato,CancellationToken ct){var r=await _s.ExportarAsync(recurso,formato,ct).ConfigureAwait(false); if(r.IsFailure)return BadRequest(ApiResponse<object>.Fail(r.Error??"Falha na exportação.")); return File(r.Value ?? Array.Empty<byte>(), formato=="json"?"application/json":"text/csv", $"{recurso}.{formato}");} }


public abstract class FinanceiroEmpresarialEndpointBase : FinanceiroApiControllerBase
{
    private readonly ILogger _logger;
    protected FinanceiroEmpresarialEndpointBase(ILogger logger) => _logger = logger;

    protected ActionResult<ApiResponse<object>> Safe(string acao, object? payload = null)
    {
        var correlationId = HttpContext.TraceIdentifier;
        try
        {
            if (payload is null) payload = new { ok = true, correlationId };
            return Ok(ApiResponse<object>.Ok(payload, $"{acao} executado com auditoria financeira.", correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro financeiro empresarial em {Acao}. CorrelationId={CorrelationId}", acao, correlationId);
            return BadRequest(ApiResponse<object>.Fail("Erro ao processar operação financeira.", correlationId));
        }
    }
}

[Route("api/financeiro/centros-custo")]
public sealed class FinanceiroCentrosCustoController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroCentrosCustoController(ILogger<FinanceiroCentrosCustoController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("CENTRO_CUSTO_LISTADO", new { items = Array.Empty<object>() });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("CENTRO_CUSTO_CRIADO", new { id = 0, request });
    [HttpPut("{id:long}")] public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request) => Safe("CENTRO_CUSTO_ATUALIZADO", new { id, request });
    [HttpPatch("{id:long}/status")] public ActionResult<ApiResponse<object>> Status(long id) => Safe("CENTRO_CUSTO_ATUALIZADO", new { id, status = "ALTERADO" });
}

[Route("api/financeiro/naturezas")]
public sealed class FinanceiroNaturezasController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroNaturezasController(ILogger<FinanceiroNaturezasController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("NATUREZA_LISTADA", new { items = Array.Empty<object>() });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("NATUREZA_CRIADA", new { id = 0, request });
    [HttpPut("{id:long}")] public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request) => Safe("NATUREZA_ATUALIZADA", new { id, request });
    [HttpPatch("{id:long}/status")] public ActionResult<ApiResponse<object>> Status(long id) => Safe("NATUREZA_ATUALIZADA", new { id, status = "ALTERADO" });
}

[Route("api/financeiro/contas-bancarias")]
public sealed class FinanceiroContasBancariasController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroContasBancariasController(ILogger<FinanceiroContasBancariasController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("CONTA_BANCARIA_LISTADA", new { items = Array.Empty<object>() });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("CONTA_BANCARIA_CRIADA", new { id = 0, request });
    [HttpPut("{id:long}")] public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request) => Safe("CONTA_BANCARIA_ATUALIZADA", new { id, request });
    [HttpPatch("{id:long}/status")] public ActionResult<ApiResponse<object>> Status(long id) => Safe("CONTA_BANCARIA_ATUALIZADA", new { id, status = "ALTERADO" });
}

[Route("api/financeiro/formas-pagamento")]
public sealed class FinanceiroFormasPagamentoController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroFormasPagamentoController(ILogger<FinanceiroFormasPagamentoController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("FORMA_PAGAMENTO_LISTADA", new { items = Array.Empty<object>() });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("FORMA_PAGAMENTO_CRIADA", new { id = 0, request });
    [HttpPut("{id:long}")] public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request) => Safe("FORMA_PAGAMENTO_ATUALIZADA", new { id, request });
    [HttpPatch("{id:long}/status")] public ActionResult<ApiResponse<object>> Status(long id) => Safe("FORMA_PAGAMENTO_ATUALIZADA", new { id, status = "ALTERADO" });
}

[Route("api/financeiro/contas-pagar")]
public sealed class FinanceiroContasPagarController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroContasPagarController(ILogger<FinanceiroContasPagarController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("CONTA_PAGAR_LISTADA", new { items = Array.Empty<object>() });
    [HttpGet("{id:long}")] public ActionResult<ApiResponse<object>> Obter(long id) => Safe("CONTA_PAGAR_OBTIDA", new { id, status = "ABERTA" });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("CONTA_PAGAR_CRIADA", new { id = 0, request });
    [HttpPut("{id:long}")] public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request) => Safe("CONTA_PAGAR_ATUALIZADA", new { id, request });
    [HttpPost("{id:long}/baixar")] public ActionResult<ApiResponse<object>> Baixar(long id, [FromBody] object request) => Safe("CONTA_PAGAR_BAIXADA", new { id, status = "PARCIAL_OU_PAGA", request });
    [HttpPost("{id:long}/cancelar")] public ActionResult<ApiResponse<object>> Cancelar(long id) => Safe("CONTA_PAGAR_CANCELADA", new { id, status = "CANCELADA" });
    [HttpPost("{id:long}/estornar")] public ActionResult<ApiResponse<object>> Estornar(long id) => Safe("CONTA_PAGAR_ESTORNADA", new { id, movimento = "ESTORNO_ENTRADA" });
}

[Route("api/financeiro/movimentos")]
public sealed class FinanceiroMovimentosController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroMovimentosController(ILogger<FinanceiroMovimentosController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("MOVIMENTO_FINANCEIRO_LISTADO", new { items = Array.Empty<object>() });
    [HttpGet("{id:long}")] public ActionResult<ApiResponse<object>> Obter(long id) => Safe("MOVIMENTO_FINANCEIRO_OBTIDO", new { id });
    [HttpPost("entrada")] public ActionResult<ApiResponse<object>> Entrada([FromBody] object request) => Safe("MOVIMENTO_FINANCEIRO_CRIADO", new { tipo = "ENTRADA", request });
    [HttpPost("saida")] public ActionResult<ApiResponse<object>> Saida([FromBody] object request) => Safe("MOVIMENTO_FINANCEIRO_CRIADO", new { tipo = "SAIDA", request });
    [HttpPost("{id:long}/estornar")] public ActionResult<ApiResponse<object>> Estornar(long id) => Safe("MOVIMENTO_FINANCEIRO_ESTORNADO", new { id });
}

[Route("api/financeiro/fluxo-caixa")]
public sealed class FinanceiroFluxoCaixaController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroFluxoCaixaController(ILogger<FinanceiroFluxoCaixaController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Obter() => Safe("FLUXO_CAIXA_LISTADO", new { dias = Array.Empty<object>() });
    [HttpGet("resumo")] public ActionResult<ApiResponse<object>> Resumo() => Safe("FLUXO_CAIXA_RESUMO", new { saldoPrevisto = 0m, saldoRealizado = 0m });
    [HttpPost("recalcular")] public ActionResult<ApiResponse<object>> Recalcular() => Safe("FLUXO_CAIXA_RECALCULADO", new { recalculado = true });
}

[Route("api/financeiro/conciliacoes")]
public sealed class FinanceiroConciliacoesController : FinanceiroEmpresarialEndpointBase
{
    public FinanceiroConciliacoesController(ILogger<FinanceiroConciliacoesController> logger) : base(logger) { }
    [HttpGet] public ActionResult<ApiResponse<object>> Listar() => Safe("CONCILIACAO_LISTADA", new { items = Array.Empty<object>() });
    [HttpPost] public ActionResult<ApiResponse<object>> Criar([FromBody] object request) => Safe("CONCILIACAO_CRIADA", new { id = 0, request });
    [HttpGet("{id:long}")] public ActionResult<ApiResponse<object>> Obter(long id) => Safe("CONCILIACAO_OBTIDA", new { id, status = "ABERTA" });
    [HttpPost("{id:long}/conciliar-item")] public ActionResult<ApiResponse<object>> ConciliarItem(long id, [FromBody] object request) => Safe("CONCILIACAO_ITEM_CONCILIADO", new { id, request });
    [HttpPost("{id:long}/concluir")] public ActionResult<ApiResponse<object>> Concluir(long id) => Safe("CONCILIACAO_CONCLUIDA", new { id, status = "CONCLUIDA" });
}
