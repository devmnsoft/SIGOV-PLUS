using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Almoxarifado;
using Sigov.Application.Frotas;
using Sigov.Application.Patrimonio;
using Sigov.Web.Models.Ativos;

namespace Sigov.Web.Controllers;

/// <summary>Portal integrado dos cadastros oficiais de almoxarifado, patrimônio e frotas.</summary>
[Authorize]
public sealed class AtivosController(
    IPatrimonioService patrimonio,
    IAlmoxarifadoService almoxarifado,
    IFrotasService frotas,
    ICurrentTenant currentTenant) : Controller
{
    [HttpGet("/Ativos")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("/Ativos/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var tenantId = currentTenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
        var entidadeId = currentTenant.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório.");
        var patrimonioTask = patrimonio.ObterDashboardAsync(tenantId, ct);
        var almoxarifadoTask = almoxarifado.ObterDashboardAsync(tenantId, entidadeId, ct);
        var frotasTask = frotas.DashboardAsync(tenantId, entidadeId, ct);
        await Task.WhenAll(patrimonioTask, almoxarifadoTask, frotasTask);
        return View(new AtivosDashboardViewModel(
            await patrimonioTask, await almoxarifadoTask, await frotasTask));
    }

    [HttpGet("/Ativos/Almoxarifados")] public IActionResult Almoxarifados() => Local("/Almoxarifado/Locais");
    [HttpGet("/Ativos/Produtos")] public IActionResult Produtos() => Local("/Almoxarifado/Materiais");
    [HttpGet("/Ativos/Estoque")] public IActionResult Estoque() => Local("/Almoxarifado/Estoque");
    [HttpGet("/Ativos/Requisicoes")] public IActionResult Requisicoes() => Local("/Almoxarifado/Requisicoes");
    [HttpGet("/Ativos/Inventarios")] public IActionResult Inventarios() => Local("/Patrimonio/Inventarios");
    [HttpGet("/Ativos/Patrimonio")] public IActionResult Patrimonio() => Local("/Patrimonio/Bens");
    [HttpGet("/Ativos/Patrimonio/Create")] public IActionResult PatrimonioCreate() => Local("/Patrimonio/Bens/Novo");
    [HttpGet("/Ativos/Patrimonio/Edit/{id:long?}")] public IActionResult PatrimonioEdit(long? id) => RequiredId(id, "/Patrimonio/Bens/Editar/");
    [HttpGet("/Ativos/Patrimonio/Details/{id:long?}")] public IActionResult PatrimonioDetails(long? id) => RequiredId(id, "/Patrimonio/Bens/");
    [HttpGet("/Ativos/Patrimonio/Transferir/{id:long?}")] public IActionResult Transferir(long? id) => RequiredId(id, "/Patrimonio/Bens/Movimentar/");
    [HttpGet("/Ativos/Patrimonio/Baixar/{id:long?}")] public IActionResult Baixar(long? id) => RequiredId(id, "/Patrimonio/Bens/Baixar/");
    [HttpGet("/Ativos/Patrimonio/Depreciacao")] public IActionResult Depreciacao() => View("Catalogo", new AtivosCatalogoViewModel("Depreciação patrimonial", "A depreciação é calculada sobre valor e vida útil persistidos, sem alterar o valor de aquisição.", "/Ativos/Patrimonio"));
    [HttpGet("/Ativos/Frotas")] public IActionResult Frotas() => Local("/Frotas/Veiculos");
    [HttpGet("/Ativos/Frotas/Create")] public IActionResult FrotasCreate() => Local("/Frotas/Veiculos/Novo");
    [HttpGet("/Ativos/Frotas/Edit/{id:long?}")] public IActionResult FrotasEdit(long? id) => RequiredId(id, "/Frotas/Veiculos/Editar/");
    [HttpGet("/Ativos/Frotas/Details/{id:long?}")] public IActionResult FrotasDetails(long? id) => RequiredId(id, "/Frotas/Veiculos/Detalhe/");
    [HttpGet("/Ativos/Frotas/Abastecimentos")] public IActionResult Abastecimentos() => Local("/Frotas/Abastecimentos");
    [HttpGet("/Ativos/Frotas/Manutencoes")] public IActionResult Manutencoes() => Local("/Frotas/Manutencoes");
    [HttpGet("/Ativos/Frotas/Documentos")] public IActionResult Documentos() => Local("/Frotas/Documentos");
    [HttpGet("/Ativos/Frotas/Rotas")] public IActionResult Rotas() => Local("/Frotas/Utilizacoes");
    [HttpGet("/Ativos/Alertas")] public IActionResult Alertas() => View("Catalogo", new AtivosCatalogoViewModel("Alertas operacionais", "Vencimentos documentais, manutenção atrasada e estoque crítico são derivados dos registros oficiais.", "/Ativos/Dashboard"));
    [HttpGet("/Ativos/Relatorios")] public IActionResult Relatorios() => View("Relatorios");

    private RedirectResult Local(string url) => Redirect(url);
    private IActionResult RequiredId(long? id, string prefix) => id is > 0 ? Local(prefix + id) : BadRequest("Selecione um registro na listagem; identificadores não são informados manualmente.");
}

public sealed record AtivosCatalogoViewModel(string Titulo, string Descricao, string Retorno);
