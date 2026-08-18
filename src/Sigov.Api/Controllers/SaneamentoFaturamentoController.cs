using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saneamento.Avancado;
namespace Sigov.Api.Controllers;
[ApiController, Route("api/saneamento/faturamento")] public sealed class SaneamentoFaturamentoController : SaneamentoAvancadoControllerBase
{
 private readonly ISaneamentoFaturamentoService _service; public SaneamentoFaturamentoController(ISaneamentoFaturamentoService service, ICurrentTenant tenant, ICurrentUser user) : base(tenant,user) => _service=service;
 [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<SaneamentoAvancadoDashboardDto>>> A0(CancellationToken ct)=>Resposta(await _service.DashboardAsync(TenantId(),"saneamento_fatura",ct));
 [HttpGet("rotas-leitura")] public async Task<ActionResult<ApiResponse<object>>> A1([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_rota_leitura",filtro,ct));
 [HttpPost("rotas-leitura")] public async Task<ActionResult<ApiResponse<long>>> A2([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_rota_leitura",request,ct));
 [HttpGet("leituras")] public async Task<ActionResult<ApiResponse<object>>> A3([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_leitura",filtro,ct));
 [HttpPost("leituras")] public async Task<ActionResult<ApiResponse<long>>> A4([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_leitura",request,ct));
 [HttpGet("lotes")] public async Task<ActionResult<ApiResponse<object>>> A5([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_faturamento_lote",filtro,ct));
 [HttpPost("lotes")] public async Task<ActionResult<ApiResponse<long>>> A6([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_faturamento_lote",request,ct));
 [HttpGet("faturas")] public async Task<ActionResult<ApiResponse<object>>> A7([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_fatura",filtro,ct));
 [HttpPost("faturas")] public async Task<ActionResult<ApiResponse<long>>> A8([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_fatura",request,ct));
 [HttpGet("pagamentos")] public async Task<ActionResult<ApiResponse<object>>> A9([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_pagamento",filtro,ct));
 [HttpPost("pagamentos")] public async Task<ActionResult<ApiResponse<long>>> A10([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_pagamento",request,ct));
 [HttpGet("inadimplencia")] public async Task<ActionResult<ApiResponse<object>>> A11([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_inadimplencia",filtro,ct));
 [HttpPost("parcelamentos")] public async Task<ActionResult<ApiResponse<long>>> A12([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_parcelamento",request,ct));
 [HttpPost("leituras/{id:long}/revisar")] public async Task<ActionResult<ApiResponse<bool>>> A13(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_leitura",id,"REVISADA",request.Justificativa,ct));
 [HttpPost("leituras/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<bool>>> A14(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_leitura",id,"CANCELADA",request.Justificativa,ct));
 [HttpPost("lotes/{id:long}/gerar-faturas")] public async Task<ActionResult<ApiResponse<bool>>> A15(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_faturamento_lote",id,"GERADO",request.Justificativa,ct));
 [HttpPost("lotes/{id:long}/emitir")] public async Task<ActionResult<ApiResponse<bool>>> A16(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_faturamento_lote",id,"EMITIDO",request.Justificativa,ct));
 [HttpPost("faturas/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<bool>>> A17(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_fatura",id,"CANCELADA",request.Justificativa,ct));
 [HttpGet("relatorios/exportar-csv")] public async Task<IActionResult> Csv(CancellationToken ct)=>File(await _service.ExportarCsvAsync(TenantId(),"saneamento_rota_leitura",ct),"text/csv","saneamento-faturamento.csv");
}
