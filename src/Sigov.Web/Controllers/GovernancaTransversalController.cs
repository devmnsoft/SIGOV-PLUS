using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Governanca;
using Sigov.Web.Models.Governanca;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class GovernancaTransversalController : Controller
{
    private readonly ITransversalGovernancaService _service;
    public GovernancaTransversalController(ITransversalGovernancaService service) => _service = service;

    [HttpGet("/Pendencias")]
    public async Task<IActionResult> Pendencias(CancellationToken ct)
    {
        var data = await _service.ListarPendenciasAsync(null, null, 1, 100, ct).ConfigureAwait(false);
        return View("Central", Model("Central de Pendências", "Trabalho real em aberto no seu tenant e escopo autorizado.", data.Select(x => new CentralTransversalItem(x.Modulo, x.Titulo, x.Gravidade, x.Status, x.RotaAcao))));
    }

    [HttpGet("/Alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct)
    {
        var data = await _service.ListarAlertasAsync(null, null, 1, 100, ct).ConfigureAwait(false);
        return View("Central", Model("Central de Alertas", "Riscos operacionais, de prazo, segurança, LGPD e integração.", data.Select(x => new CentralTransversalItem(x.Modulo, x.Titulo, x.Severidade, x.Status, x.RotaAcao))));
    }

    [HttpGet("/QualidadeDados")]
    public async Task<IActionResult> QualidadeDados(CancellationToken ct)
    {
        var data = await _service.ListarQualidadeAsync(null, null, 1, 100, ct).ConfigureAwait(false);
        return View("Central", Model("Qualidade de Dados", "Inconsistências persistidas, sem interromper a operação.", data.Select(x => new CentralTransversalItem(x.Modulo, x.Descricao, x.Severidade, x.Status, x.RotaCorrecao))));
    }

    [HttpGet("/IntegracoesInternas")]
    public async Task<IActionResult> IntegracoesInternas(CancellationToken ct)
    {
        var data = await _service.ListarIntegracoesAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Integrações Internas", "Eventos reais e integrações preparatórias identificadas explicitamente.", data.Select(x => new CentralTransversalItem(x.Origem, x.Origem + " → " + x.Destino, x.Preparatoria ? "PREPARATÓRIA" : "REAL", x.Status, x.RotaCorrecao))));
    }

    [HttpGet("/Modulos/StatusFuncional")]
    public async Task<IActionResult> StatusFuncional(CancellationToken ct)
    {
        var data = await _service.ListarStatusFuncionalAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Status Funcional por Módulo", "Estado calculado por inspeção de estrutura; o que não foi comprovado permanece pendente.", data.Select(x => new CentralTransversalItem(x.Modulo, x.Comprovacao, x.Dashboard ? "DASHBOARD" : "SEM DASHBOARD", x.StatusFinal, null))));
    }

    private static CentralTransversalViewModel Model(string title, string description, IEnumerable<CentralTransversalItem> items) =>
        new() { Titulo = title, Descricao = description, Itens = items.ToArray() };
}
