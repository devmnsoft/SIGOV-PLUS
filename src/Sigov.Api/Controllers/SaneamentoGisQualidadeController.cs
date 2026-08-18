using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saneamento.Avancado;
namespace Sigov.Api.Controllers;
[ApiController, Route("api/saneamento/gis-qualidade")] public sealed class SaneamentoGisQualidadeController : SaneamentoAvancadoControllerBase
{
 private readonly ISaneamentoGisQualidadeService _service; public SaneamentoGisQualidadeController(ISaneamentoGisQualidadeService service, ICurrentTenant tenant, ICurrentUser user) : base(tenant,user) => _service=service;
 [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<SaneamentoAvancadoDashboardDto>>> A0(CancellationToken ct)=>Resposta(await _service.DashboardAsync(TenantId(),"saneamento_qualidade_alerta",ct));
 [HttpGet("unidades-operacionais")] public async Task<ActionResult<ApiResponse<object>>> A1([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_unidade_operacional",filtro,ct));
 [HttpPost("unidades-operacionais")] public async Task<ActionResult<ApiResponse<long>>> A2([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_unidade_operacional",request,ct));
 [HttpGet("pontos")] public async Task<ActionResult<ApiResponse<object>>> A3([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_ponto_gis",filtro,ct));
 [HttpPost("pontos")] public async Task<ActionResult<ApiResponse<long>>> A4([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_ponto_gis",request,ct));
 [HttpGet("redes")] public async Task<ActionResult<ApiResponse<object>>> A5([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_rede",filtro,ct));
 [HttpPost("redes")] public async Task<ActionResult<ApiResponse<long>>> A6([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_rede",request,ct));
 [HttpGet("parametros")] public async Task<ActionResult<ApiResponse<object>>> A7([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_laboratorio_parametro",filtro,ct));
 [HttpPost("parametros")] public async Task<ActionResult<ApiResponse<long>>> A8([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_laboratorio_parametro",request,ct));
 [HttpGet("pontos-coleta")] public async Task<ActionResult<ApiResponse<object>>> A9([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_laboratorio_ponto_coleta",filtro,ct));
 [HttpPost("pontos-coleta")] public async Task<ActionResult<ApiResponse<long>>> A10([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_laboratorio_ponto_coleta",request,ct));
 [HttpGet("amostras")] public async Task<ActionResult<ApiResponse<object>>> A11([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_laboratorio_amostra",filtro,ct));
 [HttpPost("amostras")] public async Task<ActionResult<ApiResponse<long>>> A12([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_laboratorio_amostra",request,ct));
 [HttpGet("alertas")] public async Task<ActionResult<ApiResponse<object>>> A13([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_qualidade_alerta",filtro,ct));
 [HttpPost("amostras/{id:long}/aprovar")] public async Task<ActionResult<ApiResponse<bool>>> A14(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_laboratorio_amostra",id,"APROVADA",request.Justificativa,ct));
 [HttpPost("amostras/{id:long}/reprovar")] public async Task<ActionResult<ApiResponse<bool>>> A15(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_laboratorio_amostra",id,"REPROVADA",request.Justificativa,ct));
 [HttpGet("relatorios/exportar-csv")] public async Task<IActionResult> Csv(CancellationToken ct)=>File(await _service.ExportarCsvAsync(TenantId(),"saneamento_unidade_operacional",ct),"text/csv","saneamento-gis-qualidade.csv");
}
