using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Relatorios;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class RelatoriosExecutivosController : Controller
{
    private readonly IRelatorioExecutivoService _service;
    public RelatoriosExecutivosController(IRelatorioExecutivoService service) => _service = service;
    public Task<IActionResult> Dashboard([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "Executivo geral", filtro, ct);
    public Task<IActionResult> Geral([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "Executivo geral", filtro, ct);
    public Task<IActionResult> Financeiro([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "Financeiro", filtro with { Modulo="financeiro" }, ct);
    public Task<IActionResult> Tributario([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "Tributário", filtro with { Modulo="tributario" }, ct);
    public Task<IActionResult> RhFolha([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "RH / Folha (agregado)", filtro with { Modulo="rh-folha" }, ct);
    public Task<IActionResult> Educacao([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Render("Dashboard", "Educação (agregado)", filtro with { Modulo="educacao" }, ct);
    private async Task<IActionResult> Render(string view,string titulo,RelatorioExecutivoFiltro filtro,CancellationToken ct) { ViewBag.Titulo=titulo; ViewBag.Filtro=filtro; return View(view,await _service.ObterAsync(filtro,ct).ConfigureAwait(false)); }
}
