using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Orcamento;
using Sigov.Application.Rh.Previdencia;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Api.Controllers;

// RC-EVO Ciclo 2 Sprint 13 (BR-13.1 a BR-13.8): endpoints tipados da estacao de orcamento e
// responsabilidade fiscal multi-esfera (Lei 4.320/64; LC 101/2000 arts. 20/26/32/51/54; CF/88
// art. 212 por esfera). Autorizacao fail-closed por chave pontuada (banco = autoridade, regra
// 11); falhas nomeadas do dominio mapeadas explicitamente para 400/404/409 (regra 13).
// O ano do exercicio e explicito por chamada (FromQuery); exercicio ausente -> 404 nomeado.
[ApiController, Authorize, Route("api/orcamento")]
public sealed class OrcamentoLrfController(IOrcamentoService s, ICurrentTenant tenant, ICurrentUser user, IAuthorizationEvaluator auth) : ControllerBase
{
    static class P
    {
        public const string Ver = "orcamento.visualizar";
        public const string EstruturaCriar = "orcamento.estrutura.criar";
        public const string DotacaoCriar = "orcamento.dotacao.criar";
        public const string CreditoRegistrar = "orcamento.credito.registrar";
        public const string EmpenhoAbrir = "orcamento.empenho.abrir";
        public const string EmpenhoAnular = "orcamento.empenho.anular";
        public const string LiquidacaoRegistrar = "orcamento.liquidacao.registrar";
        public const string LiquidacaoAnular = "orcamento.liquidacao.anular";
        public const string ReceitaArrecadar = "orcamento.receita.arrecadar";
        public const string LrfApurar = "orcamento.lrf.apurar";
        public const string PrevisaoGerar = "orcamento.previsao.gerar";
        public const string RreoGerar = "orcamento.rreo.gerar";
        public const string ExercicioEncerrar = "orcamento.exercicio.encerrar";
    }

    OrcamentoContext X() => new(tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório."), tenant.EntidadeId, tenant.ExercicioId, user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório."), Trace());

    // visao geral
    [HttpGet("dashboard")] public Task<IActionResult> Dashboard([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.DashboardAsync(X(), ano, c), c);

    // BR-13.1 estrutura (funcao/subfuncao sao catalogos nacionais globais)
    [HttpGet("catalogo-funcional")] public Task<IActionResult> CatalogoFuncional(CancellationToken c) => Run(P.Ver, () => s.CatalogoFuncionalAsync(X(), c), c);
    [HttpGet("programas")] public Task<IActionResult> Programas([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarProgramasAsync(X(), ano, c), c);
    [HttpPost("programas")] public Task<IActionResult> CriarPrograma([FromQuery] int ano, [FromBody] ProgramaCriarRequest r, CancellationToken c) => Run(P.EstruturaCriar, async () => await s.CriarProgramaAsync(X(), ano, r, c), c);
    [HttpGet("acoes")] public Task<IActionResult> Acoes([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarAcoesAsync(X(), ano, c), c);
    [HttpPost("acoes")] public Task<IActionResult> CriarAcao([FromQuery] int ano, [FromBody] AcaoCriarRequest r, CancellationToken c) => Run(P.EstruturaCriar, async () => await s.CriarAcaoAsync(X(), ano, r, c), c);

    // BR-13.2 dotacao + creditos adicionais (sem rollover entre exercicios)
    [HttpGet("dotacoes")] public Task<IActionResult> Dotacoes([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarDotacoesAsync(X(), ano, c), c);
    [HttpPost("dotacoes")] public Task<IActionResult> CriarDotacao([FromQuery] int ano, [FromBody] DotacaoCriarRequest r, CancellationToken c) => Run(P.DotacaoCriar, async () => await s.CriarDotacaoAsync(X(), ano, r, c), c);
    [HttpPost("creditos")] public Task<IActionResult> RegistrarCredito([FromQuery] int ano, [FromBody] CreditoRegistrarRequest r, CancellationToken c) => Run(P.CreditoRegistrar, async () => await s.RegistrarCreditoAsync(X(), ano, r, c), c);

    // BR-13.3 empenho CP (disponibilidade suficiente + teto LRF pos-movimento)
    [HttpGet("empenhos")] public Task<IActionResult> Empenhos([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarEmpenhosAsync(X(), ano, c), c);
    [HttpGet("empenhos/{id:long}")] public Task<IActionResult> DetalharEmpenho(long id, [FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.DetalharEmpenhoAsync(X(), ano, id, c), c);
    [HttpPost("empenhos")] public Task<IActionResult> AbrirEmpenho([FromQuery] int ano, [FromBody] EmpenhoAbrirRequest r, CancellationToken c) => Run(P.EmpenhoAbrir, async () => await s.AbrirEmpenhoAsync(X(), ano, r, c), c);
    [HttpPost("empenhos/{id:long}/anular")] public Task<IActionResult> AnularEmpenho(long id, [FromQuery] int ano, [FromBody] EmpenhoAnularRequest r, CancellationToken c) => Run(P.EmpenhoAnular, async () => await s.AnularEmpenhoAsync(X(), ano, new EmpenhoAnularRequest(id, r.Justificativa), c), c);

    // BR-13.4 liquidacao SL (<= empenho disponivel por item)
    [HttpGet("liquidacoes")] public Task<IActionResult> Liquidacoes([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarLiquidacoesAsync(X(), ano, c), c);
    [HttpGet("liquidacoes/{id:long}")] public Task<IActionResult> DetalharLiquidacao(long id, [FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.DetalharLiquidacaoAsync(X(), ano, id, c), c);
    [HttpPost("liquidacoes")] public Task<IActionResult> RegistrarLiquidacao([FromQuery] int ano, [FromBody] LiquidacaoRegistrarRequest r, CancellationToken c) => Run(P.LiquidacaoRegistrar, async () => await s.RegistrarLiquidacaoAsync(X(), ano, r, c), c);
    [HttpPost("liquidacoes/{id:long}/anular")] public Task<IActionResult> AnularLiquidacao(long id, [FromQuery] int ano, [FromBody] LiquidacaoAnularRequest r, CancellationToken c) => Run(P.LiquidacaoAnular, async () => await s.AnularLiquidacaoAsync(X(), ano, new LiquidacaoAnularRequest(id, r.Justificativa), c), c);

    // receita prevista/arrecadada (base real das apuracoes)
    [HttpGet("receitas")] public Task<IActionResult> Receitas([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarReceitasAsync(X(), ano, c), c);
    [HttpPost("receitas")] public Task<IActionResult> CriarReceita([FromQuery] int ano, [FromBody] ReceitaCriarRequest r, CancellationToken c) => Run(P.EstruturaCriar, async () => await s.CriarReceitaAsync(X(), ano, r, c), c);
    [HttpPost("receitas/arrecadar")] public Task<IActionResult> ArrecadarReceita([FromQuery] int ano, [FromBody] ReceitaArrecadarRequest r, CancellationToken c) => Run(P.ReceitaArrecadar, async () => await s.ArrecadarReceitaAsync(X(), ano, r, c), c);

    // BR-13.5 limites LRF por esfera (override por tenant) + apuracao versionada
    [HttpGet("lrf/limites")] public Task<IActionResult> Limites(CancellationToken c) => Run(P.Ver, () => s.LimitesEfetivosAsync(X(), c), c);
    [HttpGet("lrf/apuracoes")] public Task<IActionResult> Apuracoes([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarApuracoesAsync(X(), ano, c), c);
    [HttpPost("lrf/apurar")] public Task<IActionResult> ApurarLrf([FromQuery] int ano, [FromBody] LrfApurarRequest r, CancellationToken c) => Run(P.LrfApurar, async () => await s.ApurarLrfAsync(X(), ano, r, c), c);

    // BR-13.6 previsao bimestral (LC 101/2000 art. 51)
    [HttpGet("previsoes-bimestrais")] public Task<IActionResult> Previsoes([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarPrevisoesAsync(X(), ano, c), c);
    [HttpPost("previsoes-bimestrais")] public Task<IActionResult> GerarPrevisao([FromQuery] int ano, [FromBody] PrevisaoGerarRequest r, CancellationToken c) => Run(P.PrevisaoGerar, async () => await s.GerarPrevisaoBimestralAsync(X(), ano, r, c), c);

    // BR-13.7 RREO semestral/anual (LC 101/2000 art. 54)
    [HttpGet("rreo")] public Task<IActionResult> Rreos([FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.ListarRreosAsync(X(), ano, c), c);
    [HttpGet("rreo/{loteId:long}")] public Task<IActionResult> DetalharRreo(long loteId, [FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.DetalharRreoAsync(X(), ano, loteId, c), c);
    [HttpPost("rreo")] public Task<IActionResult> GerarRreo([FromQuery] int ano, [FromBody] RreoGerarRequest r, CancellationToken c) => Run(P.RreoGerar, async () => await s.GerarRreoAsync(X(), ano, r, c), c);

    // BR-13.8 competencia fechada
    [HttpGet("exercicios")] public Task<IActionResult> Exercicios(CancellationToken c) => Run(P.Ver, () => s.ListarExerciciosAsync(X(), c), c);
    [HttpPost("exercicios/encerrar")] public Task<IActionResult> EncerrarExercicio([FromBody] ExercicioEncerrarRequest r, CancellationToken c) => Run(P.ExercicioEncerrar, async () => await s.EncerrarExercicioAsync(X(), r, c), c);
    [HttpPost("exercicios/reabrir")] public Task<IActionResult> ReabrirExercicio([FromBody] ExercicioReabrirRequest r, CancellationToken c) => Run(P.ExercicioEncerrar, async () => await s.ReabrirExercicioAsync(X(), r, c), c);

    // parametros efetivos da frente (override por tenant sobre o padrao institucional)
    [HttpGet("parametros")] public Task<IActionResult> Parametros(CancellationToken c) => Run(P.Ver, () => s.ParametrosEfetivosAsync(X(), c), c);

    async Task<IActionResult> Run<T>(string p, Func<Task<T>> f, CancellationToken c)
    {
        if (!await Allowed(p, c)) return Forbid();
        return await Exec(f);
    }

    async Task<bool> Allowed(string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), "orcamento", p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "API_C2S13"), c)).Permitido;
    }

    long T() => tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    long E() => tenant.EntidadeId ?? 0L;
    long U() => user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório.");
    string Trace() => HttpContext.TraceIdentifier;

    async Task<IActionResult> Exec<T>(Func<Task<T>> f)
    {
        try { return Ok(ApiResponse<T>.Ok(await f())); }
        // ---- 404: recurso ausente / autoridade inexistente (regra 13) ----
        catch (KeyNotFoundException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoRecursoNaoDisponivelException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoExercicioInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoProgramaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoAcaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoDotacaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenhoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenhoItemInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoLiquidacaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoReceitaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoSubfuncaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoFuncaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoRreoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        // ---- 400: validacao de entrada / configuracao ausente ----
        catch (ArgumentException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (EntidadeSemEsferaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoAcaoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoPoderInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoCategoriaEconomicaInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoGrupoDespesaInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoCreditoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenhoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoReceitaNaturezaInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoBimestreInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoRreoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoSemestreRreoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (EmpenhoSemItensException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (LiquidacaoSemItensException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoJustificativaInsuficienteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoArrecadacaoInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoRreoPeriodoInconsistenteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        // ---- 409: conflito de estado / backstop / gate da frente ----
        catch (InvalidOperationException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoProgramaJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoAcaoJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoDotacaoJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoReceitaJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoExercicioEncerradoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoExercicioNaoEstavaEncerradoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenheiroInsuficienteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoLiquidacaoSuperEmpenhoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoLrfLimiteBloqueadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenhoJaAnuladoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoLiquidacaoJaAnuladaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoEmpenhoComLiquidacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (OrcamentoLiquidacaoSobreEmpenhoAnuladoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
    }
}
