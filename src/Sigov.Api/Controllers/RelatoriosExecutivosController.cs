using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Relatorios;

namespace Sigov.Api.Controllers;

[ApiController, Route("api/relatorios-executivos"), RequireModule("relatorios_executivos")]
public sealed class RelatoriosExecutivosController : ControllerBase
{
    private readonly IRelatorioExecutivoService _service; private readonly IRelatorioExecutivoWidgetService _widgets; private readonly IRelatorioExecutivoExportService _export; private readonly IRelatorioExecutivoRepository _repository; private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user;
    public RelatoriosExecutivosController(IRelatorioExecutivoService service, IRelatorioExecutivoWidgetService widgets, IRelatorioExecutivoExportService export, IRelatorioExecutivoRepository repository, ICurrentTenant tenant, ICurrentUser user) { _service=service; _widgets=widgets; _export=export; _repository=repository; _tenant=tenant; _user=user; }
    [HttpGet("dashboard"), HttpGet("geral")] public async Task<ActionResult<ApiResponse<RelatorioExecutivoDashboardDto>>> Dashboard([FromQuery] RelatorioExecutivoFiltro filtro,CancellationToken ct) => Ok(ApiResponse<RelatorioExecutivoDashboardDto>.Ok(await _service.ObterAsync(filtro,ct).ConfigureAwait(false),correlationId:HttpContext.TraceIdentifier));
    [HttpGet("financeiro")] public async Task<ActionResult<ApiResponse<RelatorioExecutivoFinanceiroDto>>> Financeiro([FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) { var d=await _service.ObterAsync(f,ct).ConfigureAwait(false); return Ok(ApiResponse<RelatorioExecutivoFinanceiroDto>.Ok(d.Financeiro,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("tributario")] public async Task<ActionResult<ApiResponse<RelatorioExecutivoTributarioDto>>> Tributario([FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) { var d=await _service.ObterAsync(f,ct).ConfigureAwait(false); return Ok(ApiResponse<RelatorioExecutivoTributarioDto>.Ok(d.Tributario,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("rh-folha")] public async Task<ActionResult<ApiResponse<RelatorioExecutivoRhFolhaDto>>> RhFolha([FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) { var d=await _service.ObterAsync(f,ct).ConfigureAwait(false); return Ok(ApiResponse<RelatorioExecutivoRhFolhaDto>.Ok(d.RhFolha,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("educacao")] public async Task<ActionResult<ApiResponse<RelatorioExecutivoEducacaoDto>>> Educacao([FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) { var d=await _service.ObterAsync(f,ct).ConfigureAwait(false); return Ok(ApiResponse<RelatorioExecutivoEducacaoDto>.Ok(d.Educacao,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("widgets")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RelatorioExecutivoWidgetDto>>>> Widgets([FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) => Ok(ApiResponse<IReadOnlyCollection<RelatorioExecutivoWidgetDto>>.Ok(await _widgets.ListarAsync(f,ct).ConfigureAwait(false),correlationId:HttpContext.TraceIdentifier));
    [HttpPost("filtros-salvos")] public async Task<ActionResult<ApiResponse<long>>> SalvarFiltro([FromBody] RelatorioExecutivoFiltroSalvoRequest r,CancellationToken ct) { var id=await _repository.SalvarFiltroAsync(_tenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório."),_user.UsuarioId,r,HttpContext.TraceIdentifier,ct).ConfigureAwait(false); return Ok(ApiResponse<long>.Ok(id,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("filtros-salvos")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Filtros(CancellationToken ct) { var itens=await _repository.ListarFiltrosAsync(_tenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório."),_user.UsuarioId,ct).ConfigureAwait(false); return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(itens,correlationId:HttpContext.TraceIdentifier)); }
    [HttpGet("exportar-{formato:regex(^(csv|json)$)}")] public async Task<IActionResult> Exportar(string formato,[FromQuery] RelatorioExecutivoFiltro f,CancellationToken ct) { var x=await _export.ExportarAsync(formato,f,ct).ConfigureAwait(false); return File(x.Conteudo,x.ContentType,x.Nome); }
}
