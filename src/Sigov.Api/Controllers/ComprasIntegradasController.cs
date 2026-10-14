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

    // Sprint 9 (BR-7.5 a BR-7.7): fornecimento -> recebimento -> fatura -> destinacao -> obrigacao.
    // Recebimento consome a chave aprovar/rejeitar conforme a parte presente (ambas no misto);
    // destinacao consome as chaves reutilizadas do catalogo (almoxarifado/patrimonio, modulo proprio).
    [HttpGet("fornecimentos/{id:long}")] public Task<IActionResult> Fornecimento(long id, CancellationToken c) => Run(P.FornecimentoVer, () => s.FornecimentoAsync(X(), id, c), c);
    [HttpPost("fornecimentos")] public Task<IActionResult> CriarFornecimento([FromBody] FornecimentoCriarRequest r, CancellationToken c) => Run(P.FornecimentoCriar, async () => new { id = await s.FornecimentoCriarAsync(X(), r, c) }, c);
    [HttpPost("fornecimentos/{id:long}/recebimentos")] public Task<IActionResult> Recebimento(long id, [FromBody] RecebimentoIntegradoRegistrarRequest r, CancellationToken c)
        => RunMulti(r.QuantidadeAprovada > 0 && r.QuantidadeRejeitada > 0 ? new[] { P.RecebAprovar, P.RecebRejeitar } : r.QuantidadeRejeitada > 0 ? new[] { P.RecebRejeitar } : new[] { P.RecebAprovar }, () => s.RecebimentoIntegradoRegistrarAsync(X(), id, r, c), c);
    [HttpPost("recebimentos/{id:long}/desfecho")] public Task<IActionResult> Desfecho(long id, [FromBody] RecebimentoDesfechoRequest r, CancellationToken c) => Run(P.RecebRejeitar, () => s.RecebimentoDesfechoAsync(X(), id, r, c), c);
    [HttpPost("fornecimentos/{id:long}/fatura")] public Task<IActionResult> Fatura(long id, [FromBody] FaturaVincularRequest r, CancellationToken c) => Run(P.FaturaVincular, () => s.FaturaVincularAsync(X(), id, r, c), c);
    [HttpPost("destinacoes")] public async Task<IActionResult> Destinar([FromBody] DestinacaoIntegradaRequest r, CancellationToken c)
    {
        var patrimonial = (r.AlvoTipo ?? "").Trim().ToUpperInvariant() == "PATRIMONIO";
        if (!await AllowedMod(patrimonial ? "patrimonio" : "almoxarifado", patrimonial ? P.PatrimonioBemCriar : P.AlmoxRecebConfirmar, c)) return Forbid();
        return await Exec<DestinacaoIntegradaDto>(() => s.DestinacaoRegistrarAsync(X(), r, c));
    }
    [HttpPost("obrigacoes/gerar")] public Task<IActionResult> GerarObrigacao([FromBody] ObrigacaoGerarRequest r, CancellationToken c) => Run(P.ObrigacaoLiquidar, () => s.ObrigacaoGerarAsync(X(), r, c), c);
    [HttpPost("obrigacoes/{id:long}/liquidar")] public Task<IActionResult> LiquidarObrigacao(long id, CancellationToken c) => Run(P.ObrigacaoLiquidar, () => s.ObrigacaoLiquidarAsync(X(), id, c), c);
    [HttpPost("obrigacoes/{id:long}/estornar")] public Task<IActionResult> EstornarObrigacao(long id, [FromBody] ObrigacaoEstornarBody b, CancellationToken c) => Run(P.ObrigacaoLiquidar, () => s.ObrigacaoEstornarAsync(X(), id, b.Justificativa, c), c);

    // Sprint 10 (BR-7.8 + BR-10.x): vinculos juridico + convenios/obras (etapa 3/3).
    // Chaves dos modulos juridico/convenios/obras avaliadas por AllowedMod (modulo proprio);
    // contrapartida/vinculo/quitar reutilizam chaves canonicas do catalogo C2S7 (modulo compras).
    [HttpPost("juridico/consultas")] public Task<IActionResult> CriarConsultaJuridica([FromBody] ConsultaJuridicaCriarRequest r, CancellationToken c) => RunMod("juridico", P.JuridicoConsultaCriar, () => s.ConsultaJuridicaCriarAsync(X(), r, c), c);
    [HttpPost("juridico/consultas/{id:long}/parecer")] public Task<IActionResult> EmitirParecer(long id, [FromBody] ParecerEmitirRequest r, CancellationToken c) => RunMod("juridico", P.JuridicoParecerEmitir, () => s.ParecerEmitirAsync(X(), id, r, c), c);
    [HttpPost("juridico/condicoes/{id:long}/satisfazer")] public Task<IActionResult> SatisfazerCondicao(long id, [FromBody] CondicaoSatisfazerBody b, CancellationToken c) => RunMod("juridico", P.JuridicoParecerEmitir, () => s.CondicaoSatisfazerAsync(X(), id, b.Justificativa, c), c);
    [HttpPost("juridico/consultas/{id:long}/filial")] public Task<IActionResult> AbrirFilial(long id, [FromBody] ConsultaFilialBody b, CancellationToken c) => RunMod("juridico", P.JuridicoConsultaCriar, () => s.ConsultaFilialAsync(X(), id, b.Justificativa, c), c);
    [HttpPost("juridico/contenciosos")] public Task<IActionResult> AbrirContencioso([FromBody] ContenciosoAbrirRequest r, CancellationToken c) => RunMod("juridico", P.JuridicoContenciosoAbrir, () => s.ContenciosoAbrirAsync(X(), r, c), c);
    [HttpPost("juridico/contenciosos/{id:long}/desfecho")] public Task<IActionResult> DesfecharContencioso(long id, [FromBody] ContenciosoDesfechoBody b, CancellationToken c) => RunMod("juridico", P.JuridicoContenciosoDesfechar, () => s.ContenciosoDesfecharAsync(X(), id, new ContenciosoDesfecharRequest(b.Resultado, b.Custas, b.Justificativa), c), c);
    [HttpPost("contrapartidas/convenio")] public Task<IActionResult> RegistrarContrapartida([FromBody] ContrapartidaRegistrarRequest r, CancellationToken c) => Run(P.ContrapartidaRegistrar, () => s.ContrapartidaRegistrarAsync(X(), r, c), c);
    [HttpPost("vinculos/convenio")] public Task<IActionResult> AplicarVinculoConvenio([FromBody] VinculoConvenioAplicarRequest r, CancellationToken c) => Run(P.ContrapartidaRegistrar, () => s.VinculoConvenioAplicarAsync(X(), r, c), c);
    [HttpGet("vinculos")] public Task<IActionResult> Vinculos(long contratoId, CancellationToken c) => Run(P.AuditoriaVer, () => s.VinculosContratoAsync(X(), contratoId, c), c);
    [HttpPost("vinculos/{id:long}/quietar")] public Task<IActionResult> QuitarVinculo(long id, [FromBody] VinculoQuitarBody b, CancellationToken c) => Run(P.ContratoExecucaoFiscalizar, () => s.VinculoQuitarAsync(X(), id, b.Justificativa, c), c);
    [HttpPost("convenios/instrumentos/{id:long}/encerrar")] public Task<IActionResult> EncerrarInstrumento(long id, [FromBody] ConvencioEncerrarBody b, CancellationToken c) => RunMod("convenios", P.ConveniosInstrumentoEncerrar, () => s.ConvencioEncerrarAsync(X(), id, b?.Justificativa, c), c);
    [HttpPost("convenios/prestacoes")] public Task<IActionResult> RegistrarPrestacao([FromBody] PrestacaoRegistrarRequest r, CancellationToken c) => RunMod("convenios", P.ConveniosPrestacaoRegistrar, () => s.PrestacaoRegistrarAsync(X(), r, c), c);
    [HttpPost("obras/medicoes/{id:long}/homologar")] public Task<IActionResult> HomologarMedicao(long id, CancellationToken c) => RunMod("obras", P.ObrasMedicaoRegistrar, () => s.MedicaoHomologarAsync(X(), id, c), c);

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
            ContratoExecucaoFiscalizar = "compras.contrato.execucao.fiscalizar",
            // Sprint 9 (BR-7.5 a BR-7.7)
            FornecimentoCriar = "compras.fornecimento.criar",
            FornecimentoVer = "compras.fornecimento.visualizar",
            RecebAprovar = "compras.recebimento.aprovar",
            RecebRejeitar = "compras.recebimento.rejeitar",
            FaturaVincular = "compras.fatura.vincular",
            ObrigacaoLiquidar = "compras.obrigacao.liquidar",
            AlmoxRecebConfirmar = "almoxarifado.recebimento.confirmar",
            PatrimonioBemCriar = "patrimonio.bem.criar",
            // Sprint 10 (BR-7.8 + BR-10.x): reutilizacoes do catalogo C2S7 + chaves dos modulos vinculados
            ContrapartidaRegistrar = "compras.contrapartida.registrar",
            AuditoriaVer = "compras.auditoria.visualizar",
            JuridicoConsultaCriar = "juridico.consulta.criar",
            JuridicoParecerEmitir = "juridico.parecer.emitir",
            JuridicoContenciosoAbrir = "juridico.contencioso.abrir",
            JuridicoContenciosoDesfechar = "juridico.contencioso.desfechar",
            ConveniosInstrumentoEncerrar = "convenios.instrumento.encerrar",
            ConveniosPrestacaoRegistrar = "convenios.prestacao.registrar",
            ObrasMedicaoRegistrar = "obras.medicao.registrar";
    }

    async Task<IActionResult> Run<T>(string p, Func<Task<T>> f, CancellationToken c)
    {
        if (!await Allowed(p, c)) return Forbid();
        return await Exec<T>(f);
    }

    async Task<IActionResult> RunMulti<T>(string[] ps, Func<Task<T>> f, CancellationToken c)
    {
        foreach (var p in ps) if (!await Allowed(p, c)) return Forbid();
        return await Exec<T>(f);
    }

    // Sprint 10: autorizacao por modulo proprio (juridico/convenios/obras) quando a chave nao e do modulo compras.
    async Task<IActionResult> RunMod<T>(string mod, string p, Func<Task<T>> f, CancellationToken c)
    {
        if (!await AllowedMod(mod, p, c)) return Forbid();
        return await Exec<T>(f);
    }

    async Task<bool> AllowedMod(string mod, string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), mod, p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "API_C2S9"), c)).Permitido;
    }

    async Task<IActionResult> Exec<T>(Func<Task<T>> f)
    {
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
        catch (FornecimentoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoItemInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (RecebimentoIntegradoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ObrigacaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (DestinoLocalInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (DestinoMaterialInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaItemInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DemandaUrgenciaSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DispensaBaseLegalObrigatoriaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (HabilitacaoReprovacaoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (JulgamentoEmpateSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoDiretaSemFornecedorException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AdjudicacaoSemItensLicitadosException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (RescisaoSemAtoJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (RecebimentoRejeicaoSemMotivoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (RejecaoDesfechoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (FaturaNaturezaInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (FaturaFornecedorDiscrepanteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (FaturaValorDivergenteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DestinoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
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
        // Sprint 10 (BR-7.8 + BR-10.x) - 404
        catch (ProcessoComprasInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ConsultaJuridicaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (CondicaoParecerInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ProcessoJuridicoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (InstrumentoConvenioInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ObraInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (MedicaoObrasInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoExternoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        // Sprint 10 - 400
        catch (ConsultaTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ParecerConclusaoInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ParecerCondicionadoSemCondicaoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ParecerCondicaoIndevidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (SatisfazerCondicaoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ConsultaSemProcessoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ContenciosoOrigemInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (CasoContenciosoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ContenciosoResultadoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DesfechoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (QuitarVinculoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaValorInconsistenteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaRealizacaoSemDataException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (InstrumentoEmRascunhoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (EncerrarConvenioSaldoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (PrestacaoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (MedicaoStatusInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        // Sprint 10 - 409 (gates da frente + conflitos de estado/idempotencia)
        catch (ParecerCondicaoAbertaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ProrrogacaoBloqueadaContenciosoAtivoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ObraContratoSemProjetoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaConvenioInexistenteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ObraMedicaoInsuficienteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ConsultaChaveIdempotenciaConflitoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FilialDuplicadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ConsultaPaiNaoResolvidaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ParecerJaEmitidoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (CondicaoJaSatisfeitaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContenciosoAtivoParaOrigemException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContenciosoJaDesfecadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaChaveIdempotenciaConflitoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContrapartidaExcedeInstrumentoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (InstrumentoEncerradoImutavelException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (InstrumentoNaoAtivoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoConvenioChaveConflitoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ConvenioEncerramentoBloqueadoContratoAtivoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (InstrumentoJaEncerradoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PrestacaoExecucaoMinimaNaoAtingidaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PrestacaoNumeroDuplicadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (PrestacaoFinalSaldoNaoJustificadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoJaQuitadoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (MedicaoJaHomologadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ContratoNaoVigenteParaVinculoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
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
    public sealed record ObrigacaoEstornarBody(string Justificativa);
    // Sprint 10 (BR-7.8 + BR-10.x)
    public sealed record CondicaoSatisfazerBody(string Justificativa);
    public sealed record ConsultaFilialBody(string Justificativa);
    public sealed record ContenciosoDesfechoBody(string Resultado, decimal Custas, string Justificativa);
    public sealed record VinculoQuitarBody(string Justificativa);
    public sealed record ConvencioEncerrarBody(string? Justificativa = null);
}
