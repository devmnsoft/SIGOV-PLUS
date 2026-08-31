using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Executive;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Executivo")]
public sealed class ExecutivoController(ICentralExecutivaService service, ICurrentUser user) : Controller
{
    [HttpGet("")][HttpGet("Dashboard")][Authorize(Policy="EXECUTIVO_DASHBOARD_VIEW")]
    public async Task<IActionResult> Index([FromQuery] ExecutivoFiltro filtro,CancellationToken ct) { ViewData["Filtro"]=filtro; return View("Index",await service.DashboardAsync(filtro,ct)); }
    [HttpGet("SalaSituacao")][Authorize(Policy="EXECUTIVO_SALA_VIEW")] public Task<IActionResult> SalaSituacao([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("salasituacao","Sala de Situação Municipal",f,ct);
    [HttpGet("Metas")][Authorize(Policy="EXECUTIVO_META_VIEW")] public Task<IActionResult> Metas([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("metas","Metas do Plano de Governo",f,ct);
    [HttpGet("Pendencias")][Authorize(Policy="EXECUTIVO_PENDENCIA_VIEW")] public Task<IActionResult> Pendencias([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("pendencias","Pendências transversais",f,ct);
    [HttpGet("Alertas")][Authorize(Policy="EXECUTIVO_ALERTA_VIEW")] public Task<IActionResult> Alertas([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("alertas","Alertas executivos",f,ct);
    [HttpGet("Encaminhamentos")][Authorize(Policy="EXECUTIVO_PENDENCIA_VIEW")] public Task<IActionResult> Encaminhamentos([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("encaminhamentos","Encaminhamentos",f,ct);
    [HttpGet("Aprovacoes")][Authorize(Policy="EXECUTIVO_APROVACAO_VIEW")] public Task<IActionResult> Aprovacoes([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("aprovacoes","Aprovações executivas",f,ct);
    [HttpGet("Decisoes")][Authorize(Policy="EXECUTIVO_DECISAO_VIEW")] public Task<IActionResult> Decisoes([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("decisoes","Decisões executivas",f,ct);
    [HttpGet("Briefing")][Authorize(Policy="EXECUTIVO_BRIEFING_VIEW")] public Task<IActionResult> Briefing([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("briefing","Briefing diário",f,ct);
    [HttpGet("Relatorios")][Authorize(Policy="EXECUTIVO_RELATORIO_EXPORT")] public Task<IActionResult> Relatorios([FromQuery] ExecutivoFiltro f,CancellationToken ct)=>Lista("indicadores","Relatórios gerenciais",f,ct);
    [HttpGet("Configuracoes")][Authorize(Policy="EXECUTIVO_DASHBOARD_VIEW")] public IActionResult Configuracoes()=>View();
    [HttpPost("Alertas/{id:long}/Ciencia")][ValidateAntiForgeryToken][Authorize(Policy="EXECUTIVO_ALERTA_MANAGE")] public async Task<IActionResult> Ciencia(long id,CancellationToken ct){await service.MarcarAlertaCienteAsync(id,user.UsuarioId,ct);return RedirectToAction(nameof(Alertas));}
    [HttpPost("Aprovacoes/{id:long}/Decidir")][ValidateAntiForgeryToken][Authorize(Policy="EXECUTIVO_APROVACAO_MANAGE")] public async Task<IActionResult> Decidir(long id,bool aprovar,string justificativa,CancellationToken ct){if(string.IsNullOrWhiteSpace(justificativa)){TempData["Erro"]="Informe a justificativa.";return RedirectToAction(nameof(Aprovacoes));}await service.DecidirAprovacaoAsync(id,aprovar,justificativa,user.UsuarioId,ct);return RedirectToAction(nameof(Aprovacoes));}
    [HttpGet("Exportar/{recurso}")][Authorize(Policy="EXECUTIVO_RELATORIO_EXPORT")] public async Task<IActionResult> Exportar(string recurso,[FromQuery] ExecutivoFiltro f,CancellationToken ct){var bytes=await service.ExportarCsvAsync(recurso,f,user.UsuarioId,ct);return File(bytes,"text/csv; charset=utf-8",$"executivo-{recurso}-{DateTime.UtcNow:yyyyMMdd}.csv");}
    private async Task<IActionResult> Lista(string recurso,string titulo,ExecutivoFiltro filtro,CancellationToken ct){ViewData["Title"]=titulo;ViewData["Recurso"]=recurso;ViewData["Filtro"]=filtro;return View("Lista",await service.ListarAsync(recurso,filtro,ct));}
}
