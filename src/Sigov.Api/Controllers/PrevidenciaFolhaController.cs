using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Rh.Previdencia;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Api.Controllers;

// RC-EVO Ciclo 2 Sprint 12 (BR-12.1 a BR-12.8): endpoints tipados da estacao de folha e
// previdencia publica multi-esfera com o maximo de regras da legislacao brasileira
// parametrizadas no banco (CF/88 art. 37 XV/XVI e 40; EC 103/2019; LC 51/1984; Lei 8.112/1990;
// CLT arts. 129-151; Lei 4.749/65 c/c Lei 12.735/2012; Decreto 3.048/99; Portaria MTP 672/2019).
// Autorizacao fail-closed por chave pontuada (banco = autoridade, regra 11); falhas nomeadas do
// dominio mapeadas explicitamente para 400/404/409 (regra 13).
[ApiController, Authorize, Route("api/previdencia")]
public sealed class PrevidenciaFolhaController(IPrevidenciaFolhaService s, ICurrentTenant tenant, ICurrentUser user, IAuthorizationEvaluator auth) : ControllerBase
{
    static class P
    {
        public const string Ver = "previdencia.visualizar";
        public const string CargoCriar = "previdencia.cargo.criar";
        public const string ServidorCriar = "previdencia.servidor.criar";
        public const string FolhaFechar = "previdencia.folha.fechar";
        public const string FolhaReprocessar = "previdencia.folha.reprocessar";
        public const string FeriasApurar = "previdencia.ferias.apurar";
        public const string FeriasProgramar = "previdencia.ferias.programar";
        public const string AfastamentoRegistrar = "previdencia.afastamento.registrar";
        public const string ContrachequeVer = "previdencia.contracheque.visualizar";
        public const string EsocialEnviar = "previdencia.esocial.enviar";
    }

    PrevidenciaContext X() => new(tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório."), tenant.EntidadeId, tenant.ExercicioId, user.UsuarioId ?? throw new InvalidOperationException("Usuário obrigatório."), Trace());

    // BR-12.1 cadastro multi-esfera
    [HttpGet("dashboard")] public Task<IActionResult> Dashboard(CancellationToken c) => Run(P.Ver, () => s.DashboardAsync(X(), c), c);
    [HttpGet("cargos")] public Task<IActionResult> Cargos(CancellationToken c) => Run(P.Ver, () => s.ListarCargosAsync(X(), c), c);
    [HttpPost("cargos")] public Task<IActionResult> CriarCargo([FromBody] CargoCriarRequest r, CancellationToken c) => Run(P.CargoCriar, async () => await s.CriarCargoAsync(X(), r, c), c);
    [HttpGet("servidores")] public Task<IActionResult> Servidores(CancellationToken c) => Run(P.Ver, () => s.ListarServidoresAsync(X(), c), c);
    [HttpPost("servidores")] public Task<IActionResult> AdmitirServidor([FromBody] ServidorAdmitirRequest r, CancellationToken c) => Run(P.ServidorCriar, async () => await s.AdmitirServidorAsync(X(), r, c), c);
    [HttpPost("remuneracoes")] public Task<IActionResult> RegistrarRemuneracao([FromBody] RemuneracaoRegistrarRequest r, CancellationToken c) => Run(P.ServidorCriar, async () => await s.RegistrarRemuneracaoAsync(X(), r, c), c);
    [HttpPost("desligamentos")] public Task<IActionResult> DesligarServidor([FromBody] DesligamentoRequest r, CancellationToken c) => Run(P.ServidorCriar, async () => new { id = await s.DesligarServidorAsync(X(), r, c) }, c);

    // BR-12.2/12.5/12.6 folha deterministica + teto constitucional por esfera
    [HttpPost("folhas/abrir")] public Task<IActionResult> AbrirFolha([FromBody] FolhaCompetenciaRequest r, CancellationToken c) => Run(P.FolhaFechar, async () => await s.AbrirCompetenciaAsync(X(), r, c), c);
    [HttpPost("folhas/fechar")] public Task<IActionResult> FecharFolha([FromBody] FolhaCompetenciaRequest r, CancellationToken c) => Run(P.FolhaFechar, async () => await s.FecharFolhaAsync(X(), r, c), c);
    [HttpPost("folhas/reabrir")] public Task<IActionResult> ReabrirFolha([FromBody] FolhaReabrirRequest r, CancellationToken c) => Run(P.FolhaFechar, async () => await s.ReabrirFolhaAsync(X(), r, c), c);

    // BR-12.3 reprocesso versionado (nova versao veta a anterior; ambas auditaveis)
    [HttpPost("folhas/reprocessar")] public Task<IActionResult> ReprocessarCalculo([FromBody] ReprocessarCalculoRequest r, CancellationToken c) => Run(P.FolhaReprocessar, async () => await s.ReprocessarCalculoAsync(X(), r, c), c);
    [HttpGet("calculos")] public Task<IActionResult> Calculos([FromQuery] long vinculoId, [FromQuery] DateOnly competencia, CancellationToken c) => Run(P.Ver, () => s.ListarCalculosAsync(X(), vinculoId, competencia, c), c);

    // BR-12.7 contracheque versionado (LGPD: dados minimos)
    [HttpGet("contracheque")] public Task<IActionResult> Contracheque([FromQuery] long vinculoId, [FromQuery] DateOnly competencia, CancellationToken c) => Run(P.ContrachequeVer, () => s.ObterContrachequeAsync(X(), vinculoId, competencia, c), c);

    // BR-12.4 ferias + afastamentos
    [HttpPost("ferias/apurar-aquisicao")] public Task<IActionResult> ApurarAquisicao([FromBody] FeriasVinculoRequest r, CancellationToken c) => Run(P.FeriasApurar, async () => await s.ApurarAquisicaoFeriasAsync(X(), r, c), c);
    [HttpPost("ferias/programar")] public Task<IActionResult> ProgramarFerias([FromBody] FeriasProgramarRequest r, CancellationToken c) => Run(P.FeriasProgramar, async () => await s.ProgramarFeriasAsync(X(), r, c), c);
    [HttpPost("ferias/{id:long}/gozo")] public Task<IActionResult> RegistrarGozo(long id, [FromBody] FeriasGozoRequest r, CancellationToken c) => Run(P.FeriasProgramar, async () => new { id = await s.RegistrarGozoFeriasAsync(X(), r, c) }, c);
    [HttpPost("afastamentos")] public Task<IActionResult> RegistrarAfastamento([FromBody] AfastamentoRegistrarRequest r, CancellationToken c) => Run(P.AfastamentoRegistrar, async () => await s.RegistrarAfastamentoAsync(X(), r, c), c);
    [HttpPost("afastamentos/{id:long}/devolver")] public Task<IActionResult> DevolverAfastamento(long id, [FromBody] AfastamentoDevolverRequest r, CancellationToken c) => Run(P.AfastamentoRegistrar, async () => await s.DevolverAfastamentoAsync(X(), new AfastamentoDevolverRequest(id, r.DataFixa, r.Motivo), c), c);

    // 13o salario (situacao anual auditavel)
    [HttpGet("decimo")] public Task<IActionResult> Decimo([FromQuery] long vinculoId, [FromQuery] int ano, CancellationToken c) => Run(P.Ver, () => s.SituacaoDecimoAsync(X(), vinculoId, ano, c), c);

    // parametros efetivos da frente (override por tenant sobre o padrao institucional)
    [HttpGet("parametros")] public Task<IActionResult> Parametros(CancellationToken c) => Run(P.Ver, () => s.ParametrosEfetivosAsync(X(), c), c);

    // BR-12.8 eSocial publico (lote idempotente por competencia; validacao previa ao envio)
    [HttpPost("esocial/gerar-lote")] public Task<IActionResult> GerarLoteEsocial([FromBody] EsocialLoteGerarRequest r, CancellationToken c) => Run(P.EsocialEnviar, async () => await s.GerarLoteEsocialAsync(X(), r, c), c);
    [HttpPost("esocial/{loteId:long}/enviar")] public Task<IActionResult> EnviarLoteEsocial(long loteId, CancellationToken c) => Run(P.EsocialEnviar, async () => await s.EnviarLoteEsocialAsync(X(), loteId, c), c);
    [HttpGet("esocial/lotes/{loteId:long}")] public Task<IActionResult> DetalharLoteEsocial(long loteId, CancellationToken c) => Run(P.Ver, () => s.DetalharLoteEsocialAsync(X(), loteId, c), c);

    async Task<IActionResult> Run<T>(string p, Func<Task<T>> f, CancellationToken c)
    {
        if (!await Allowed(p, c)) return Forbid();
        return await Exec(f);
    }

    async Task<bool> Allowed(string p, CancellationToken c)
    {
        var i = p.LastIndexOf('.');
        return (await auth.EvaluateAsync(new(U(), "previdencia", p[..i], p[(i + 1)..], T(), E(), tenant.ExercicioId, null, null, Trace(), "API_C2S12"), c)).Permitido;
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
        catch (CargoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (ServidorInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (FolhaCompetenciaInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (FeriasCicloInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (AfastamentoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (LoteEsocialInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (CalculoVigenteInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (NaturezaRemuneracaoInexistenteException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        catch (PrevidenciaRecursoNaoDisponivelException e) { return NotFound(ApiResponse<T>.Fail(e.Message)); }
        // ---- 400: validacao de entrada / configuracao ausente ----
        catch (ArgumentException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (EntidadeSemEsferaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (CompetenciaInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DataFixaInvalidaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (PeriodoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AfastamentoTipoInvalidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DocumentoGedObrigatoriaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AfastamentoDuracaoInconsistenteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ReaberturaSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (ReprocessoSemJustificativaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (IntervaloFeriasInsuficienteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (FracaoFeriasInsuficienteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (AbonoPecuniarioMinimoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (SaldoFeriasInsuficienteException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (FracionamentoFeriasExcedidoException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        catch (DevolucaoAfastamentoAntecipadaException e) { return BadRequest(ApiResponse<T>.Fail(e.Message)); }
        // ---- 409: conflito de estado / backstop / gate da frente ----
        catch (InvalidOperationException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (CargoJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (MatriculaFuncionalDuplicadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoAtivoExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (VinculoNaoAtivoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (RemuneracaoJaExisteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FolhaJafecadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FolhaNaoEstavaFechadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (ServidorAcimaDoTetoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (SemCicloAquisitivoNovoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FeriasProgramacaoNaoProgramadaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (AfastamentoSobrepostoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (AfastamentoJaDevolvidoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (DecimoSemPrimeiraParcelaException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (FolhaNaoFechadaParaLoteException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
        catch (LoteEsocialBloqueadoValidacaoException e) { return Conflict(ApiResponse<T>.Fail(e.Message)); }
    }
}
