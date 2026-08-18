using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saneamento.Avancado;
namespace Sigov.Api.Controllers;
[ApiController, Route("api/saneamento/comercial")] public sealed class SaneamentoComercialController : SaneamentoAvancadoControllerBase
{
 private readonly ISaneamentoComercialService _service; public SaneamentoComercialController(ISaneamentoComercialService service, ICurrentTenant tenant, ICurrentUser user) : base(tenant,user) => _service=service;
 [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<SaneamentoAvancadoDashboardDto>>> A0(CancellationToken ct)=>Resposta(await _service.DashboardAsync(TenantId(),"saneamento_consumidor",ct));
 [HttpGet("consumidores")] public async Task<ActionResult<ApiResponse<object>>> A1([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_consumidor",filtro,ct));
 [HttpPost("consumidores")] public async Task<ActionResult<ApiResponse<long>>> A2([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_consumidor",request,ct));
 [HttpGet("ligacoes")] public async Task<ActionResult<ApiResponse<object>>> A3([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_ligacao",filtro,ct));
 [HttpPost("ligacoes")] public async Task<ActionResult<ApiResponse<long>>> A4([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_ligacao",request,ct));
 [HttpGet("hidrometros")] public async Task<ActionResult<ApiResponse<object>>> A5([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_hidrometro",filtro,ct));
 [HttpPost("hidrometros")] public async Task<ActionResult<ApiResponse<long>>> A6([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_hidrometro",request,ct));
 [HttpGet("tarifas")] public async Task<ActionResult<ApiResponse<object>>> A7([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_tarifa",filtro,ct));
 [HttpPost("tarifas")] public async Task<ActionResult<ApiResponse<long>>> A8([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_tarifa",request,ct));
 [HttpGet("atendimentos")] public async Task<ActionResult<ApiResponse<object>>> A9([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_atendimento",filtro,ct));
 [HttpPost("atendimentos")] public async Task<ActionResult<ApiResponse<long>>> A10([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_atendimento",request,ct));
 [HttpPost("ligacoes/{id:long}/alterar-status")] public async Task<ActionResult<ApiResponse<bool>>> A11(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ligacao",id,"ATIVA",request.Justificativa,ct));
 [HttpPost("atendimentos/{id:long}/encerrar")] public async Task<ActionResult<ApiResponse<bool>>> A12(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_atendimento",id,"ENCERRADO",request.Justificativa,ct));
 [HttpPost("ligacoes/{id:long}/instalar-hidrometro")] public async Task<ActionResult<ApiResponse<long>>> A13(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_hidrometro",request with { LigacaoId=id, Status="INSTALADO" },ct));
 [HttpPost("ligacoes/{id:long}/substituir-hidrometro")] public async Task<ActionResult<ApiResponse<bool>>> A14(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_hidrometro",id,"SUBSTITUIDO",request.Justificativa,ct));
 [HttpGet("relatorios/exportar-csv")] public async Task<IActionResult> Csv(CancellationToken ct)=>File(await _service.ExportarCsvAsync(TenantId(),"saneamento_consumidor",ct),"text/csv","saneamento-comercial.csv");
}
