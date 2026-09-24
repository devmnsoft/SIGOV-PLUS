using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Governanca;
using Sigov.Application.Abstractions;
using Sigov.Web.Models.Governanca;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class GovernancaTransversalController : Controller
{
    private readonly ITransversalGovernancaService _service;
    private readonly ICurrentTenant _tenant;
    private readonly ILogger<GovernancaTransversalController> _logger;
    public GovernancaTransversalController(ITransversalGovernancaService service, ICurrentTenant tenant, ILogger<GovernancaTransversalController> logger)
        => (_service, _tenant, _logger) = (service, tenant, logger);

    [HttpGet("/Pendencias")]
    public async Task<IActionResult> Pendencias(string visao = "MINHAS", string? modulo = null, string? situacao = null,
        string? gravidade = null, long? responsavelUsuarioId = null, string? prazo = null, DateOnly? aberturaDe = null,
        DateOnly? aberturaAte = null, DateOnly? encerramentoDe = null, DateOnly? encerramentoAte = null,
        int pagina = 1, int tamanho = 25, string ordenacao = "PRIORIDADE", CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired("Central de Pendências", "pendencia");
        var filtro = new PendenciaOperacionalFiltro(visao, modulo, situacao, gravidade, responsavelUsuarioId, prazo,
            aberturaDe, aberturaAte, encerramentoDe, encerramentoAte, pagina, tamanho, ordenacao);
        var data = await _service.ListarPendenciasAsync(filtro, ct).ConfigureAwait(false);
        var model = Model("Fila operacional", "Trabalho autorizado para atribuir, acompanhar, executar na origem e consultar após o encerramento.", "pendencia", modulo, gravidade, data.Pagina, data.Tamanho,
            data.Itens.Select(x => new CentralTransversalItem(x.Id, x.Modulo, x.Titulo, x.Gravidade, x.Status, x.RotaAcao, x.Descricao, x.ResponsavelNome ?? "Não atribuído", x.Prazo, Abertura: x.CreatedAt)));
        return View("Central", new CentralTransversalViewModel { Titulo=model.Titulo, Descricao=model.Descricao, Tipo=model.Tipo,
            ContextoSelecionado=model.ContextoSelecionado, ContextoNome=model.ContextoNome, Modulo=modulo, Classificacao=gravidade,
            Pagina=data.Pagina, Tamanho=data.Tamanho, Itens=model.Itens, Total=data.Total, TemProximaPagina=data.TemProximaPagina,
            Visao=visao, Situacao=situacao, Prazo=prazo, ResponsavelUsuarioId=responsavelUsuarioId, AberturaDe=aberturaDe,
            AberturaAte=aberturaAte, EncerramentoDe=encerramentoDe, EncerramentoAte=encerramentoAte,
            Ordenacao=data.Ordenacao, Indicadores=data.Indicadores });
    }

    [HttpGet("/Alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired("Central de Alertas", "alerta");
        var data = await _service.ListarAlertasAsync(null, null, 1, 100, ct).ConfigureAwait(false);
        return View("Central", Model("Central de Alertas", "Riscos operacionais, de prazo, segurança, LGPD e integração.", "alerta", null, null, 1, 100, data.Select(x => new CentralTransversalItem(x.Id, x.Modulo, x.Titulo, x.Severidade, x.Status, x.RotaAcao))));
    }

    [HttpGet("/Governanca/QualidadeDados")]
    public async Task<IActionResult> QualidadeDados(string? modulo, string? severidade, int pagina = 1, int tamanho = 25, CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired("Qualidade de Dados", "qualidade");
        var data = await _service.ListarQualidadeAsync(modulo, severidade, pagina, tamanho, ct).ConfigureAwait(false);
        return View("Central", Model("Qualidade de Dados", "Diagnósticos persistidos e explicáveis; a correção é confirmada nos dados de origem.", "qualidade", modulo, severidade, pagina, tamanho,
            data.Select(x => new CentralTransversalItem(x.Id, x.Modulo, x.Descricao, x.Severidade, x.Status, x.RotaCorrecao, $"Regra {x.Regra} · {x.Entidade} {x.EntidadeId}", x.ResponsavelUsuarioId is null ? "Não atribuído" : $"Responsável #{x.ResponsavelUsuarioId}", VerificadoEm: x.VerificadoEm ?? x.DetectedAt))));
    }

    [HttpGet("/Governanca/IntegracoesInternas")]
    public async Task<IActionResult> IntegracoesInternas(CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired("Integrações Internas", "integracao");
        var data = await _service.ListarIntegracoesAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Integrações Internas", "Eventos reais e integrações preparatórias identificadas explicitamente.", "integracao", null, null, 1, 100, data.Select(x => new CentralTransversalItem(0, x.Origem, x.Origem + " → " + x.Destino, x.Preparatoria ? "PREPARATÓRIA" : "REAL", x.Status, x.RotaCorrecao))));
    }

    [HttpGet("/QualidadeDados")]
    public IActionResult QualidadeDadosAlias() => RedirectToActionPermanent(nameof(QualidadeDados));

    [HttpGet("/IntegracoesInternas")]
    public IActionResult IntegracoesInternasAlias() => RedirectToActionPermanent(nameof(IntegracoesInternas));

    [HttpGet("/Modulos/StatusFuncional")]
    public async Task<IActionResult> StatusFuncional(CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired("Status Funcional por Módulo", "status");
        var data = await _service.ListarStatusFuncionalAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Status Funcional por Módulo", "Estado calculado por inspeção de estrutura; o que não foi comprovado permanece pendente.", "status", null, null, 1, 100, data.Select(x => new CentralTransversalItem(0, x.Modulo, x.Comprovacao, x.Dashboard ? "DASHBOARD" : "SEM DASHBOARD", x.StatusFinal, null))));
    }

    [HttpGet("/Governanca/Ocorrencias/{tipo}/{id:long}")]
    public async Task<IActionResult> Detalhe(string tipo, long id, string? retorno, string? buscaResponsavel,
        int paginaResponsavel = 1, CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired("Detalhe da ocorrência", tipo);
        var item = await _service.ObterOcorrenciaAsync(tipo, id, ct).ConfigureAwait(false);
        if (item is null) return NotFound();
        return View("Detalhe", await CriarDetalheAsync(item, retorno, buscaResponsavel, paginaResponsavel, null, null, false, ct).ConfigureAwait(false));
    }

    [ValidateAntiForgeryToken, HttpPost("/Governanca/Ocorrencias/{tipo}/{id:long}/atribuir")]
    public async Task<IActionResult> Atribuir(string tipo, long id, long responsavelUsuarioId, long versao, string justificativa, string? retorno, CancellationToken ct)
    {
        if (!HasContext()) return Forbid();
        if (responsavelUsuarioId <= 0 || string.IsNullOrWhiteSpace(justificativa) || justificativa.Trim().Length > 1000)
        {
            var itemInvalido = await _service.ObterOcorrenciaAsync(tipo, id, ct).ConfigureAwait(false);
            if (itemInvalido is null) return NotFound();
            TempData["Error"] = "Selecione uma pessoa elegível e informe uma justificativa de até 1000 caracteres.";
            return View("Detalhe", await CriarDetalheAsync(itemInvalido, retorno, null, 1, responsavelUsuarioId,
                justificativa, false, ct).ConfigureAwait(false));
        }
        var result = await _service.AtribuirAsync(tipo, id, responsavelUsuarioId, versao, justificativa, ct).ConfigureAwait(false);
        if (!result.Sucesso)
        {
            var item = await _service.ObterOcorrenciaAsync(tipo, id, ct).ConfigureAwait(false);
            if (item is null) return NotFound();
            TempData["Error"] = result.Mensagem;
            return View("Detalhe", await CriarDetalheAsync(item, retorno, null, 1, responsavelUsuarioId, justificativa,
                result.Codigo == "CONFLITO", ct).ConfigureAwait(false));
        }
        TempData["Toast"] = result.Mensagem;
        return RedirectToAction(nameof(Detalhe), new { tipo, id, retorno = LocalReturn(retorno) });
    }

    [ValidateAntiForgeryToken, HttpPost("/Governanca/Ocorrencias/qualidade/{id:long}/revalidar")]
    public async Task<IActionResult> Revalidar(long id, long versao, string? retorno, CancellationToken ct)
    {
        if (!HasContext()) return Forbid();
        var result = await _service.RevalidarQualidadeAsync(id, versao, ct).ConfigureAwait(false);
        TempData[result.Sucesso ? "Toast" : "Error"] = result.Mensagem;
        return RedirectToAction(nameof(Detalhe), new { tipo = "qualidade", id, retorno = LocalReturn(retorno) });
    }

    private bool HasContext() => _tenant.TenantId is > 0;
    private async Task<GovernancaOcorrenciaViewModel> CriarDetalheAsync(GovernancaOcorrenciaDto item, string? retorno,
        string? buscaResponsavel, int paginaResponsavel, long? responsavelInformado, string? justificativaInformada,
        bool conflito, CancellationToken ct)
    {
        const int tamanho = 20;
        paginaResponsavel = Math.Max(1, paginaResponsavel);
        IReadOnlyCollection<ResponsavelElegivelDto> responsaveis = [];
        var podeAtribuir = true;
        var temProxima = false;
        try
        {
            var resultado = await _service.BuscarResponsaveisAsync(buscaResponsavel, paginaResponsavel, tamanho, ct).ConfigureAwait(false);
            temProxima = resultado.TemProximaPagina;
            responsaveis = resultado.Itens;
            var selecionado = responsavelInformado ?? item.ResponsavelUsuarioId;
            if (selecionado.HasValue && responsaveis.All(x => x.UsuarioId != selecionado.Value))
            {
                var elegivel = await _service.ObterResponsavelElegivelAsync(selecionado.Value, ct).ConfigureAwait(false);
                if (elegivel is not null) responsaveis = responsaveis.Append(elegivel).ToArray();
            }
        }
        catch (UnauthorizedAccessException) { podeAtribuir = false; }
        return new GovernancaOcorrenciaViewModel
        {
            Ocorrencia = item, Retorno = LocalReturn(retorno), Responsaveis = responsaveis, PodeAtribuir = podeAtribuir,
            BuscaResponsavel = buscaResponsavel, PaginaResponsavel = paginaResponsavel,
            TemProximaPaginaResponsavel = temProxima, ResponsavelInformado = responsavelInformado,
            JustificativaInformada = justificativaInformada, Conflito = conflito,
            ResponsavelInformadoElegivel = !responsavelInformado.HasValue || responsaveis.Any(x => x.UsuarioId == responsavelInformado),
            DraftKey = $"sigov:governanca:{_tenant.TenantId}:{User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}:{item.Tipo}:{item.Id}"
        };
    }
    private static string? LocalReturn(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal) ? value : null;
    private IActionResult ContextRequired(string title, string type)
    {
        _logger.LogWarning("Contexto operacional não selecionado. Route={Route} Actor={Actor} Stage={Stage} Result={Result} CorrelationId={CorrelationId}",
            Request.Path, User.Identity?.Name, "tenant-resolution", "selection-required", HttpContext.TraceIdentifier);
        return View("Central", Model(title, "Selecione um contexto autorizado antes de consultar dados operacionais.", type, null, null, 1, 25, []));
    }
    private CentralTransversalViewModel Model(string title, string description, string type, string? modulo, string? classificacao, int pagina, int tamanho, IEnumerable<CentralTransversalItem> items) =>
        new() { Titulo = title, Descricao = description, Tipo = type, ContextoSelecionado = HasContext(), ContextoNome = User.FindFirst("tenant_name")?.Value ?? $"Contexto #{_tenant.TenantId}", Modulo = modulo, Classificacao = classificacao, Pagina = Math.Max(1, pagina), Tamanho = Math.Clamp(tamanho, 1, 100), Itens = items.ToArray() };
}
