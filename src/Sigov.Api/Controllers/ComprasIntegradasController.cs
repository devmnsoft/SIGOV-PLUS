using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Compras.Integradas;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Api.Controllers;

// RC-EVO Ciclo 2 Sprint 8 (BR-8.1 a BR-8.7): endpoints tipados da frente integrada de compras
// publica multi-esfera. Autorizacao fail-closed por chave de permissao (banco = autoridade, regra 11);
// falhas nomeadas do dominio mapeadas explicitamente para 400/404/409 (regra 13).
[ApiController, Authorize, Route("api/compras/integradas")]
public sealed class ComprasIntegradasController(IComprasIntegradasService s, ICurrentTenant tenant, ICurrentUser user, IAuthorizationEvaluator auth) : ControllerBase
{
    ComprasIntegradasContext X() => new(tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório."), tenant.EntidadeId, tenant.ExercicioId, user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório."), Trace());

    [HttpGet("demandas")] public Task<IActionResult> Demandas(CancellationToken c) => Run(P.DemandaVer, () => s.DemandasAsync(X(), c), c);
    [HttpGet("demandas/{id:long}")] public Task<IActionResult> Demanda(long id, CancellationToken c) => Run(P.DemandaVer, () => s.DemandaAsync(X(), id, c), c);
    [HttpPost("demandas")] public Task<IActionResult> CriarDemanda([FromBody] DemandaCriarRequest r, CancellationToken c) => Run(P.DemandaCriar, async () => new { id = await s.CriarDemandaAsync(X(), r, c) }, c);
    [HttpPost("demandas/{id:long}/aprovar")] public Task<IActionResult> Aprovar(long id, CancellationToken c) => Run(P.DemandaAprovar, () => s.DemandaAprovarAsync(X(), id, c), c);
    [HttpPost("demandas/{id:long}/repelir")] public Task<IActionResult> Repelir(long id, [FromBody] DemandaOperarRequest r, CancellationToken c) => Run(P.DemandaAprovar, () => s.DemandaRepelirAsync(X(), id, r.Justificativa, c), c);
    [HttpPost("demandas/{id:long}/reabrir")] public Task<IActionResult> Reabrir(long id, [FromBody] DemandaOperarRequest r, CancellationToken c) => Run(P.DemandaReabrir, () => s.DemandaReabrirAsync(X(), id, r.Justificativa, c), c);
    [HttpPost("demandas/{id:long}/cancelar")] public Task<IActionResult> Cancelar(long id, [FromBody] DemandaOperarRequest r, CancellationToken c) => Run(P.DemandaAprovar, () => s.DemandaCancelarAsync(X(), id, r.Justificativa, c), c);

    [HttpPost("procedimentos/abrir")] public Task<IActionResult> AbrirProcedimento([FromBody] ProcedimentoAbrirRequest r, CancellationToken c) => Run(P.LicitacaoAbrir, () => s.ProcedimentoAbrirAsync(X(), r, c), c);
    [HttpPost("licitacoes/{id:long}/habilitacoes")] public Task<IActionResult> Habilitacao(long id, [FromBody] HabilitacaoJulgarRequest r, CancellationToken c) => Run(P.HabilitacaoJulgar, async () => { await s.HabilitacaoJulgarAsync(X(), id, r, c); return new { id }; }, c);
    [HttpPost("licitacoes/{id:long}/lances")] public Task<IActionResult> Lance(long id, [FromBody] LanceRegistrarRequest r, CancellationToken c) => Run(P.LancesRegistrar, async () => new { id = await s.LanceRegistrarAsync(X(), id, r, c) }, c);
    [HttpPost("licitacoes/{id:long}/preferencia")] public Task<IActionResult> Preferencia(long id, [FromBody] PreferenciaDocumentarRequest r, CancellationToken c) => Run(P.PreferenciaDocumentar, () => s.PreferenciaDocumentarAsync(X(), id, r, c), c);
    [HttpPost("licitacoes/{id:long}/julgamento")] public Task<IActionResult> Julgamento(long id, [FromBody] JulgamentoBody b, CancellationToken c) => Run(P.JulgamentoExecutar, () => s.JulgamentoExecutarAsync(X(), id, b.JustificativaEmpate, c), c);
    [HttpPost("licitacoes/{id:long}/adjudicar")] public Task<IActionResult> Adjudicar(long id, [FromBody] AdjudicarRequest r, CancellationToken c) => Run(P.AdjudicacaoExecutar, () => s.AdjudicarAsync(X(), id, r, c), c);
    [HttpPost("licitacoes/{id:long}/homologar")] public Task<IActionResult> Homologar(long id, CancellationToken c) => Run(P.HomologacaoExecutar, async () => new { id = await s.HomologarAsync(X(), id, c) }, c);
    [HttpPost("licitacoes/{id:long}/propostas/{propostaId:long}/rejeitar")] public Task<IActionResult> RejeitarProposta(long id, long propostaId, [FromBody] PropostaRejeitarBody b, CancellationToken c) => Run(P.AdjudicacaoExecutar, () => s.PropostaRejeitarAsync(X(), id, propostaId, b.Motivo, c), c);

    [HttpPost("contratos")] public Task<IActionResult> GerarContrato([FromBody] ContratoGerarRequest r, CancellationToken c) => Run(P.ContratoCriar, () => s.ContratoGerarAsync(X(), r, c), c);
    [HttpGet("contratos/{id:long}/execucao")] public Task<IActionResult> Execucao(long id, CancellationToken c) => Run(P.ContratoExecucaoFiscalizar, () => s.ContratoExecucaoAsync(X(), id, c), c);
    [HttpPost("contratos/{id:long}/prorrogar")] public Task<IActionResult> Prorrogar(long id, [FromBody] ContratoProrrogarBody b, CancellationToken c) => Run(P.ContratoProrrogar, async () => { await s.ContratoProrrogarAsync(X(), id, b.NovoFim, b.Justificativa, c); return new { id }; }, c);
    [HttpPost("contratos/{id:long}/rescindir")] public Task<IActionResult> Rescindir(long id, [FromBody] ContratoRescindirBody b, CancellationToken c) => Run(P.ContratoRescindir, async () => { await s.ContratoRescindirAsync(X(), id, b.Tipo, b.Justificativa, b.NotificacaoRef, c); return new { id }; }, c);

    // chaves da seed C2S7 (banco = autoridade, regra 11)
    static class P
    {
        public const string DemandaVer = "compras.demanda.visualizar",
            DemandaCriar = "compras.demanda.criar",
            DemandaAprovar = "compras.demanda.aprovar",
            DemandaReabrir = "compras.demanda.reabrir",
            LicitacaoAbrir = "compras.licitacao.abrir",
            HabilitacaoJulgar = "compras.licitacao.habilitacao.julgar",
            LancesRegistrar = "compras.licitacao.lances.registrar",
            PreferenciaDocumentar = "compras.licitacao.preferencia.documentar",
            JulgamentoExecutar = "compras.julgamento.executar",
            AdjudicacaoExecutar = "compras.adjudicacao.executar",
            HomologacaoExecutar = "compras.homologacao.executar",
            ContratoCriar = "compras.contrato.criar",
            ContratoProrrogar = "compras.contrato.prorrogar",
            ContratoRescindir = "compras.contrato.rescindir",
            ContratoExecucaoFiscalizar = "compras.contrato.execucao.fiscalizar";
    }

    async Task<IActionResult> Run<T>(string p, Func<Task<T>> f, CancellationToken c)
    {
        if (!await Allowed(p, c)) return Forbid();
        try { return Ok(ApiResponse<T>.Ok(await f())); }
        catch (KeyNotFoundException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ArgumentException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (InvalidOperationException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (LicitacaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (PropostaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (HomologacaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (FornecedorInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ModalidadeForaDoCatalogoException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (DispensaBaseLegalInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ComprasIntegradasRecursoNaoDisponivelException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaItemInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaUrgenciaSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DispensaBaseLegalObrigatoriaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (HabilitacaoReprovacaoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (JulgamentoEmpateSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoDiretaSemFornecedorException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoSemItensLicitadosException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (RescisaoSemAtoJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaEstadoInvalidoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaUrgenciaSemDuplaAprovacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaAposAprovacaoImutavelException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (SeparacaoFuncoesVioladaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ProcessoJaVinculadoADemandaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (LicitacaoEstadoInvalidoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (SessaoLanceForaDeVigenciaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (LanceNaoDecrescenteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (LanceChaveIdempotenciaConflitoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ZonaMeEppNaoAplicavelException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ZonaMeEppJaResolvidaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FornecedorForaDaZonaMeEppException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (JulgamentoJaRealizadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (JulgamentoSemLancesException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PreferenciaMeEppNaoDocumentadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoSemJulgamentoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoJaRealizadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoSemPropostaClassificadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (HomologacaoSemAdjudicacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (HomologacaoJaRealizadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (HomologadorEEadjudicadorIguaisException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (HomologacaoPrazoExcedidoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ConsultaPreviaAcimaTetoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoJaGeradoParaHomologacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoSemProcedimentoHomologadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoNaoVigenteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ProrrogacaoExcedeTetoArt106Exception e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ProrrogacoesMaximasExcedidasException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FornecimentoExcedeExecucaoDisponivelException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PropostaJaRejeitadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PropostaRejeicaoSemJulgamentoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PropostaRejeicaoPosAdjudicacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ComprasIntegradasFalhaNomeada e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
    }

    async Task<bool> Allowed(string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), "compras", p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "API_C2S8"), c)).Permitido;
    }

    long T() => tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    long E() => tenant.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório.");
    long U() => user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório.");
    string Trace() => HttpContext.TraceIdentifier;

    public sealed record JulgamentoBody(string? JustificativaEmpate);
    public sealed record PropostaRejeitarBody(string Motivo);
    public sealed record ContratoProrrogarBody(DateOnly NovoFim, string Justificativa);
    public sealed record ContratoRescindirBody(string Tipo, string Justificativa, string? NotificacaoRef);
}
