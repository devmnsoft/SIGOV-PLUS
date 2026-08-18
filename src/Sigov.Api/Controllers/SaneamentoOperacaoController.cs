using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saneamento.Avancado;
namespace Sigov.Api.Controllers;
[ApiController, Route("api/saneamento/operacao")] public sealed class SaneamentoOperacaoController : SaneamentoAvancadoControllerBase
{
 private readonly ISaneamentoOperacaoService _service; public SaneamentoOperacaoController(ISaneamentoOperacaoService service, ICurrentTenant tenant, ICurrentUser user) : base(tenant,user) => _service=service;
 [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<SaneamentoAvancadoDashboardDto>>> A0(CancellationToken ct)=>Resposta(await _service.DashboardAsync(TenantId(),"saneamento_ordem_servico",ct));
 [HttpGet("equipes")] public async Task<ActionResult<ApiResponse<object>>> A1([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_equipe",filtro,ct));
 [HttpPost("equipes")] public async Task<ActionResult<ApiResponse<long>>> A2([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_equipe",request,ct));
 [HttpGet("ordens")] public async Task<ActionResult<ApiResponse<object>>> A3([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_ordem_servico",filtro,ct));
 [HttpPost("ordens")] public async Task<ActionResult<ApiResponse<long>>> A4([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_ordem_servico",request,ct));
 [HttpGet("cortes")] public async Task<ActionResult<ApiResponse<object>>> A5([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_corte",filtro,ct));
 [HttpPost("cortes")] public async Task<ActionResult<ApiResponse<long>>> A6([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_corte",request,ct));
 [HttpGet("religacoes")] public async Task<ActionResult<ApiResponse<object>>> A7([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_religacao",filtro,ct));
 [HttpPost("religacoes")] public async Task<ActionResult<ApiResponse<long>>> A8([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_religacao",request,ct));
 [HttpGet("vazamentos")] public async Task<ActionResult<ApiResponse<object>>> A9([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_vazamento",filtro,ct));
 [HttpPost("vazamentos")] public async Task<ActionResult<ApiResponse<long>>> A10([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_vazamento",request,ct));
 [HttpGet("vistorias")] public async Task<ActionResult<ApiResponse<object>>> A11([FromQuery] SaneamentoAvancadoFiltro filtro,CancellationToken ct)=>Resposta<object>(await _service.ListarAsync(TenantId(),"saneamento_vistoria",filtro,ct));
 [HttpPost("vistorias")] public async Task<ActionResult<ApiResponse<long>>> A12([FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.CriarAsync(Contexto(),"saneamento_vistoria",request,ct));
 [HttpPost("ordens/{id:long}/agendar")] public async Task<ActionResult<ApiResponse<bool>>> A13(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ordem_servico",id,"AGENDADA",request.Justificativa,ct));
 [HttpPost("ordens/{id:long}/iniciar")] public async Task<ActionResult<ApiResponse<bool>>> A14(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ordem_servico",id,"EM_CAMPO",request.Justificativa,ct));
 [HttpPost("ordens/{id:long}/pausar")] public async Task<ActionResult<ApiResponse<bool>>> A15(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ordem_servico",id,"PAUSADA",request.Justificativa,ct));
 [HttpPost("ordens/{id:long}/concluir")] public async Task<ActionResult<ApiResponse<bool>>> A16(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ordem_servico",id,"CONCLUIDA",request.Justificativa,ct));
 [HttpPost("ordens/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<bool>>> A17(long id,[FromBody] SaneamentoAvancadoOperacaoRequest request,CancellationToken ct)=>Resposta(await _service.AlterarStatusAsync(Contexto(),"saneamento_ordem_servico",id,"CANCELADA",request.Justificativa,ct));
 [HttpGet("relatorios/exportar-csv")] public async Task<IActionResult> Csv(CancellationToken ct)=>File(await _service.ExportarCsvAsync(TenantId(),"saneamento_equipe",ct),"text/csv","saneamento-operacao.csv");
}
