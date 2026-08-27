using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Almoxarifado;
using Sigov.Application.Frotas;
using Sigov.Application.Patrimonio;
using Sigov.Web.Models.Ativos;

namespace Sigov.Web.Controllers;

/// <summary>Porta de entrada integrada do Ativos360, sem duplicar os domínios operacionais existentes.</summary>
[Authorize]
public sealed class AtivosController(
    Sigov.Application.Patrimonio.IPatrimonioService patrimonio,
    Sigov.Application.Almoxarifado.IAlmoxarifadoService almoxarifado,
    IFrotasService frotas,
    ICurrentTenant tenant) : Controller
{
    [HttpGet("/Ativos"), HttpGet("/Ativos/Dashboard")]
    public async Task<IActionResult> Dashboard(string? status, DateOnly? inicio, DateOnly? fim, CancellationToken ct)
    {
        var patrimonioTask = patrimonio.ObterDashboardAsync(Tenant(), ct);
        var almoxarifadoTask = almoxarifado.ObterDashboardAsync(Tenant(), Entidade(), ct);
        var frotasTask = frotas.DashboardAsync(Tenant(), Entidade(), ct);
        await Task.WhenAll(patrimonioTask, almoxarifadoTask, frotasTask);
        return View(new AtivosDashboardViewModel(await patrimonioTask, await almoxarifadoTask, await frotasTask, status, inicio, fim));
    }

    [HttpGet("/Ativos/Almoxarifados")] public IActionResult Almoxarifados() => Redirect("/Almoxarifado/Locais");
    [HttpGet("/Ativos/Produtos")] public IActionResult Produtos() => Redirect("/Almoxarifado/Materiais");
    [HttpGet("/Ativos/Estoque")] public IActionResult Estoque() => Redirect("/Almoxarifado/Estoque");
    [HttpGet("/Ativos/Requisicoes")] public IActionResult Requisicoes() => Redirect("/Almoxarifado/Requisicoes");
    [HttpGet("/Ativos/Inventarios")] public IActionResult Inventarios() => Redirect("/Patrimonio/Inventarios");
    [HttpGet("/Ativos/Patrimonio")] public IActionResult Patrimonio() => Redirect("/Patrimonio/Bens");
    [HttpGet("/Ativos/Patrimonio/Create")] public IActionResult PatrimonioCreate() => Redirect("/Patrimonio/Bens/Novo");
    [HttpGet("/Ativos/Patrimonio/Edit")] public IActionResult PatrimonioEdit(long id) => Redirect($"/Patrimonio/Bens/{id}/Editar");
    [HttpGet("/Ativos/Patrimonio/Details")] public IActionResult PatrimonioDetails(long id) => Redirect($"/Patrimonio/Bens/{id}");
    [HttpGet("/Ativos/Patrimonio/Transferir")] public IActionResult Transferir(long id) => Redirect($"/Patrimonio/Bens/{id}");
    [HttpGet("/Ativos/Patrimonio/Baixar")] public IActionResult Baixar(long id) => Redirect($"/Patrimonio/Bens/{id}");
    [HttpGet("/Ativos/Patrimonio/Depreciacao")] public IActionResult Depreciacao() => View("Hub", Hub("Depreciação patrimonial", "Cálculo mensal e anual auditável dos bens com valor e vida útil.", "/Patrimonio/Bens"));
    [HttpGet("/Ativos/Frotas")] public IActionResult Frotas() => Redirect("/Frotas/Veiculos");
    [HttpGet("/Ativos/Frotas/Create")] public IActionResult FrotasCreate() => Redirect("/Frotas/Veiculos/Novo");
    [HttpGet("/Ativos/Frotas/Edit")] public IActionResult FrotasEdit(long id) => Redirect($"/Frotas/Veiculos/Editar/{id}");
    [HttpGet("/Ativos/Frotas/Details")] public IActionResult FrotasDetails(long id) => Redirect($"/Frotas/Veiculos/Detalhe/{id}");
    [HttpGet("/Ativos/Frotas/Abastecimentos")] public IActionResult Abastecimentos() => Redirect("/Frotas/Abastecimentos");
    [HttpGet("/Ativos/Frotas/Manutencoes")] public IActionResult Manutencoes() => Redirect("/Frotas/Manutencoes");
    [HttpGet("/Ativos/Frotas/Documentos")] public IActionResult Documentos() => Redirect("/Frotas/Documentos");
    [HttpGet("/Ativos/Frotas/Rotas")] public IActionResult Rotas() => Redirect("/Frotas/Utilizacoes");
    [HttpGet("/Ativos/Alertas")] public IActionResult Alertas() => View("Hub", Hub("Alertas operacionais", "Estoque crítico, documentos vencidos e manutenções atrasadas em uma única fila.", "/Ativos/Dashboard"));
    [HttpGet("/Ativos/Relatorios")] public IActionResult Relatorios() => View("Hub", Hub("Relatórios e exportações", "Exportações CSV protegidas contra fórmulas e filtradas pelo contexto da entidade.", "/Almoxarifado/Estoque/Exportar"));

    private static (string Title, string Description, string Link) Hub(string title, string description, string link) => (title, description, link);
    private long Tenant() => tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório.");
    private long Entidade() => tenant.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório.");
}
