using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Api.Contracts;
using Sigov.Application.Educacao.Avancada;

namespace Sigov.Api.Filters;

/// <summary>
/// Ciclo 2 Sprint 2 (BR-2.6): diagnóstico uniforme para schema ausente em TODOS os caminhos dos
/// controllers avançados de educação (listar/criar/status/dashboard/csv): um único 404
/// RECURSO_INDISPONIVEL com a mensagem padrão da <see cref="RecursoNaoDisponivelException"/>.
/// Atributo herdado (aplica-se apenas aos controllers da família educacao.avancada) e atua somente
/// nas exceções que o endpoint não mapeou explicitamente (leituras sem try/catch).
/// </summary>
public sealed class EducacaoFalhasNomeadasFiltro : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // Ciclo 2 Sprint 3: alem do RECURSO_INDISPONIVEL (BR-2.6), diagnostica as falhas nomeadas de
        // circulacao completa da biblioteca (BR-3.2 a BR-3.5) em leituras sem try/catch explicito.
        var excecao = context.Exception;
        int? status = excecao switch
        {
            RecursoNaoDisponivelException => StatusCodes.Status404NotFound,
            BibliotecaExemplarInexistenteException => StatusCodes.Status404NotFound,
            BibliotecaOcorrenciaInexistenteException => StatusCodes.Status404NotFound,
            BibliotecaInventarioInexistenteException => StatusCodes.Status404NotFound,
            BibliotecaInventarioItemInexistenteException => StatusCodes.Status404NotFound,
            EmprestimoTomadorInvalidoException => StatusCodes.Status400BadRequest,
            DataDevolucaoInvalidaException => StatusCodes.Status400BadRequest,
            OcorrenciaTipoInvalidoException => StatusCodes.Status400BadRequest,
            OcorrenciaMotivoObrigatorioException => StatusCodes.Status400BadRequest,
            OcorrenciaEstornoMotivoObrigatorioException => StatusCodes.Status400BadRequest,
            InventarioDesfechoInvalidoException => StatusCodes.Status400BadRequest,
            InventarioJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            InventarioQuantidadeInvalidaException => StatusCodes.Status400BadRequest,
            DesbloqueioJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            ExemplarBloqueadoException => StatusCodes.Status409Conflict,
            EmprestimoTomadorLimiteExcedidoException => StatusCodes.Status409Conflict,
            EmprestimoEncerradoPorOcorrenciaException => StatusCodes.Status409Conflict,
            RenovacaoEmprestimoNaoVigenteException => StatusCodes.Status409Conflict,
            RenovacaoReservaVigenteException => StatusCodes.Status409Conflict,
            RenovacaoLimiteExcedidoException => StatusCodes.Status409Conflict,
            ExemplarNaoBloqueadoException => StatusCodes.Status409Conflict,
            InventarioJaFechadoException => StatusCodes.Status409Conflict,
            InventarioDivergenciasAbertasException => StatusCodes.Status409Conflict,
            InventarioItemSemDivergenciaException => StatusCodes.Status409Conflict,
            // Ciclo 2 Sprint 4: transporte escolar da rota a viagem (BR-4.1 a BR-4.8)
            TransporteVeiculoInexistenteException => StatusCodes.Status404NotFound,
            TransporteMotoristaInexistenteException => StatusCodes.Status404NotFound,
            TransporteViagemInexistenteException => StatusCodes.Status404NotFound,
            TransportePresencaInexistenteException => StatusCodes.Status404NotFound,
            TransporteOcorrenciaInexistenteException => StatusCodes.Status404NotFound,
            TransporteVeiculoSituacaoInvalidaException => StatusCodes.Status400BadRequest,
            TransporteVeiculoCapacidadeInvalidaException => StatusCodes.Status400BadRequest,
            TransporteViagemCancelamentoJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            TransporteViagemSubstituicaoSemRecursoException => StatusCodes.Status400BadRequest,
            TransportePresencaStatusInvalidoException => StatusCodes.Status400BadRequest,
            TransportePresencaEstornoJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            TransporteOcorrenciaTipoInvalidoException => StatusCodes.Status400BadRequest,
            TransporteOcorrenciaGravidadeInvalidaException => StatusCodes.Status400BadRequest,
            TransporteOcorrenciaDescricaoObrigatorioException => StatusCodes.Status400BadRequest,
            TransporteOcorrenciaDesfechoRelatorioObrigatorioException => StatusCodes.Status400BadRequest,
            TransporteCustoRealizadoValorInvalidoException => StatusCodes.Status400BadRequest,
            TransporteVeiculoPlacaExistenteException => StatusCodes.Status409Conflict,
            TransporteVeiculoInaptoException => StatusCodes.Status409Conflict,
            TransporteVeiculoInspecaoVencidaException => StatusCodes.Status409Conflict,
            TransporteMotoristaHabilitacaoVencidaException => StatusCodes.Status409Conflict,
            TransporteMotoristaCategoriaIncompativelException => StatusCodes.Status409Conflict,
            TransporteViagemMonitorObrigatorioException => StatusCodes.Status409Conflict,
            TransporteViagemCapacidadeExcedidaException => StatusCodes.Status409Conflict,
            TransporteViagemVeiculoSobreposicaoException => StatusCodes.Status409Conflict,
            TransporteViagemMotoristaSobreposicaoException => StatusCodes.Status409Conflict,
            TransporteViagemOcorrenciaGraveCaronaException => StatusCodes.Status409Conflict,
            TransporteViagemEstadoInvalidoException => StatusCodes.Status409Conflict,
            TransporteViagemCancelamentoExigeOcorrenciaException => StatusCodes.Status409Conflict,
            TransportePresencaAlunoForaDaRotaException => StatusCodes.Status409Conflict,
            TransportePresencasFechadasEstornoObrigatorioException => StatusCodes.Status409Conflict,
            TransportePresencasIncompletasException => StatusCodes.Status409Conflict,
            TransportePresencasJaFechadasException => StatusCodes.Status409Conflict,
            TransporteOcorrenciaJaDesfechadaException => StatusCodes.Status409Conflict,
            TransporteOcorrenciaAltaVigenteException => StatusCodes.Status409Conflict,
            TransporteCustoRealizadoExistenteException => StatusCodes.Status409Conflict,
            // Ciclo 2 Sprint 5: FUNDEB/custos/rateio + avaliacao do magisterio + Educacenso (BR-5.1 a BR-5.7)
            CustoRateioInexistenteException => StatusCodes.Status404NotFound,
            AvaliacaoMagisterioInexistenteException => StatusCodes.Status404NotFound,
            AvaliacaoServidorInexistenteException => StatusCodes.Status404NotFound,
            FundebCompetenciaReaberturaJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            CustoRateioCriterioInvalidoException => StatusCodes.Status400BadRequest,
            CustoRateioTotalInvalidoException => StatusCodes.Status400BadRequest,
            CustoRateioDestinosInexistentesException => StatusCodes.Status400BadRequest,
            CustoRateioPesosInvalidosException => StatusCodes.Status400BadRequest,
            CustoRateioEstornoJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            AvaliacaoMagisterioPeriodoInvalidoException => StatusCodes.Status400BadRequest,
            AvaliacaoMagisterioInstrumentoObrigatorioException => StatusCodes.Status400BadRequest,
            AvaliacaoNotaInvalidoException => StatusCodes.Status400BadRequest,
            AvaliacaoReaberturaJustificativaObrigatorioException => StatusCodes.Status400BadRequest,
            EducacensoDesfechoInvalidoException => StatusCodes.Status400BadRequest,
            EducacensoMotivoObrigatorioException => StatusCodes.Status400BadRequest,
            FundebCompetenciaFechadaException => StatusCodes.Status409Conflict,
            FundebCompetenciaNaoFechadaException => StatusCodes.Status409Conflict,
            FundebAplicacaoDuplicadaException => StatusCodes.Status409Conflict,
            CustoRateioDuplicadoException => StatusCodes.Status409Conflict,
            AvaliacaoResultadoExistenteException => StatusCodes.Status409Conflict,
            AvaliacaoFechadaResultadosBloqueadosException => StatusCodes.Status409Conflict,
            AvaliacaoEstadoInvalidoException => StatusCodes.Status409Conflict,
            EducacensoInconsistenciaJaDecididaException => StatusCodes.Status409Conflict,
            EducacensoLoteEstadoInvalidoException => StatusCodes.Status409Conflict,
            EducacensoLoteAltaAbertaException => StatusCodes.Status409Conflict,
            EducacensoLoteInconsistenciasAbertasException => StatusCodes.Status409Conflict,
            _ => null,
        };
        if (status is null) return;
        context.ExceptionHandled = true;
        context.Result = new ObjectResult(ApiResponse<object>.Fail(excecao.Message, context.HttpContext?.TraceIdentifier ?? string.Empty))
        {
            StatusCode = status.Value,
        };
    }
}
