using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Integracoes;

namespace Sigov.Api.Controllers;

[ApiController]
[RequireModule("integracao")]
[Produces("application/json")]
public abstract class IntegracoesControllerBase : ProcessosControllerBase
{
    protected static IDictionary<string, string> HeaderDictionary(IHeaderDictionary headers) => headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
}

[Route("api/integracoes/api-credentials")]
public sealed class ApiCredentialsController : IntegracoesControllerBase
{
    private readonly IApiCredentialService _service; public ApiCredentialsController(IApiCredentialService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ApiCredentialResponse>>>> Listar([FromQuery] ApiCredentialFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<ApiCredentialResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<ApiCredentialCreateResponse>>> Criar([FromBody] ApiCredentialCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/revogar")] public async Task<ActionResult<ApiResponse<object>>> Revogar(long id,[FromBody] RevogarApiCredentialRequest request,CancellationToken ct)=>FromResult(await _service.RevogarAsync(id,request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/suspender")] public async Task<ActionResult<ApiResponse<object>>> Suspender(long id,CancellationToken ct)=>FromResult(await _service.SuspenderAsync(id,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/reativar")] public async Task<ActionResult<ApiResponse<object>>> Reativar(long id,CancellationToken ct)=>FromResult(await _service.ReativarAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/sistemas")]
public sealed class IntegracoesController : IntegracoesControllerBase
{
    private readonly IIntegracaoSistemaService _service; public IntegracoesController(IIntegracaoSistemaService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<IntegracaoSistemaResponse>>>> Listar([FromQuery] IntegracaoSistemaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<IntegracaoSistemaResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] IntegracaoSistemaCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id,[FromBody] IntegracaoSistemaUpdateRequest request,CancellationToken ct)=>FromResult(await _service.AtualizarAsync(id,request,ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id,CancellationToken ct)=>FromResult(await _service.ExcluirAsync(id,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/testar-dev")] public async Task<ActionResult<ApiResponse<object>>> TestarDev(long id,CancellationToken ct)=>FromResult(await _service.TestarDevAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/webhooks")]
public sealed class WebhooksController : IntegracoesControllerBase
{
    private readonly IWebhookService _service; public WebhooksController(IWebhookService service)=>_service=service;
    [HttpPost("receber/{origem}")] public async Task<ActionResult<ApiResponse<long>>> Receber(string origem,[FromBody] WebhookReceberRequest request,CancellationToken ct)=>FromResult(await _service.ReceberAsync(origem,request,HeaderDictionary(Request.Headers),HttpContext.Connection.RemoteIpAddress?.ToString(),Request.Headers["User-Agent"].ToString(),ct).ConfigureAwait(false));
    [HttpGet("recebidos")] public async Task<ActionResult<ApiResponse<PagedResult<WebhookRecebidoResponse>>>> Recebidos([FromQuery] WebhookRecebidoFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarRecebidosAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("enviados")] public async Task<ActionResult<ApiResponse<PagedResult<WebhookEnviadoResponse>>>> Enviados([FromQuery] WebhookRecebidoFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarEnviadosAsync(filtro,ct).ConfigureAwait(false));
    [HttpPost("enviar-dev")] public async Task<ActionResult<ApiResponse<long>>> EnviarDev([FromBody] WebhookEnviarRequest request,CancellationToken ct)=>FromResult(await _service.EnviarDevAsync(request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/reprocessar")] public async Task<ActionResult<ApiResponse<object>>> Reprocessar(long id,CancellationToken ct)=>FromResult(await _service.ReprocessarAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/outbox")]
public sealed class OutboxController : IntegracoesControllerBase
{
    private readonly IOutboxService _service; public OutboxController(IOutboxService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<OutboxEventoResponse>>>> Listar([FromQuery] OutboxFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<OutboxEventoResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] OutboxEventoCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/reprocessar")] public async Task<ActionResult<ApiResponse<object>>> Reprocessar(long id,[FromBody] ReprocessarOutboxRequest request,CancellationToken ct)=>FromResult(await _service.ReprocessarAsync(id,request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/dead-letter")] public async Task<ActionResult<ApiResponse<object>>> DeadLetter(long id,[FromBody] MoverDeadLetterRequest request,CancellationToken ct)=>FromResult(await _service.DeadLetterAsync(id,request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id,CancellationToken ct)=>FromResult(await _service.CancelarAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/remessas")]
public sealed class RemessasOficiaisController : IntegracoesControllerBase
{
    private readonly IRemessaOficialService _service; public RemessasOficiaisController(IRemessaOficialService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<RemessaOficialResponse>>>> Listar([FromQuery] OutboxFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<RemessaOficialResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] RemessaOficialCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/gerar-dev")] public async Task<ActionResult<ApiResponse<object>>> GerarDev(long id,[FromBody] GerarRemessaRequest request,CancellationToken ct)=>FromResult(await _service.GerarDevAsync(id,request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/enviar-dev")] public async Task<ActionResult<ApiResponse<object>>> EnviarDev(long id,[FromBody] EnviarRemessaDevRequest request,CancellationToken ct)=>FromResult(await _service.EnviarDevAsync(id,request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id,CancellationToken ct)=>FromResult(await _service.CancelarAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/certificados")]
public sealed class CertificadosDigitaisController : IntegracoesControllerBase
{
    private readonly ICertificadoDigitalService _service; public CertificadosDigitaisController(ICertificadoDigitalService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<CertificadoDigitalResponse>>>> Listar([FromQuery] OutboxFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<CertificadoDigitalResponse>>> Obter(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync(id,ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CertificadoDigitalCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false));
    [HttpPost("{id:long}/revogar")] public async Task<ActionResult<ApiResponse<object>>> Revogar(long id,CancellationToken ct)=>FromResult(await _service.RevogarAsync(id,ct).ConfigureAwait(false));
}

[Route("api/integracoes/govbr")]
public sealed class GovBrController : IntegracoesControllerBase
{
    private readonly IGovBrAdapter _adapter; public GovBrController(IGovBrAdapter adapter)=>_adapter=adapter;
    [HttpGet("configuracao")] public ActionResult<ApiResponse<object>> Configuracao()=>Ok(ApiResponse<object>.Ok(new{provider="Gov.br estrutural",realLogin=false}));
    [HttpPost("configuracao")] public ActionResult<ApiResponse<object>> Salvar([FromBody] object request)=>Ok(ApiResponse<object>.Ok(new{salvo=true,segredosMascarados=true,request}));
    [HttpPost("testar-dev")] public async Task<ActionResult<ApiResponse<object>>> TestarDev(CancellationToken ct)=>FromResult(await _adapter.TestarDevAsync(ct).ConfigureAwait(false));
}

[Route("api/integracoes/assinador")]
public sealed class AssinadorDigitalController : IntegracoesControllerBase
{
    private readonly IAssinadorDigitalService _service; public AssinadorDigitalController(IAssinadorDigitalService service)=>_service=service;
    [HttpPost("validar-estrutura")] public async Task<ActionResult<ApiResponse<object>>> Validar(CancellationToken ct)=>FromResult(await _service.ValidarEstruturaAsync(ct).ConfigureAwait(false));
    [HttpPost("assinar-dev")] public async Task<ActionResult<ApiResponse<object>>> Assinar([FromBody] object request,CancellationToken ct)=>FromResult(await _service.AssinarDevAsync(request,ct).ConfigureAwait(false));
}

[Route("api/integracoes/dashboard")]
public sealed class IntegracaoDashboardController : IntegracoesControllerBase
{
    private readonly IIntegracaoDashboardService _service; public IntegracaoDashboardController(IIntegracaoDashboardService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<IntegracaoDashboardResponse>>> Obter(CancellationToken ct)=>FromResult(await _service.ObterAsync(ct).ConfigureAwait(false));
}

[Route("api/integracoes/export")]
public sealed class IntegracaoExportacaoController : IntegracoesControllerBase
{
    private readonly IIntegracaoExportacaoService _service; public IntegracaoExportacaoController(IIntegracaoExportacaoService service)=>_service=service;
    [HttpGet("outbox.csv")] public Task<IActionResult> OutboxCsv(CancellationToken ct)=>FileResult("outbox","csv",ct);
    [HttpGet("outbox.json")] public Task<IActionResult> OutboxJson(CancellationToken ct)=>FileResult("outbox","json",ct);
    [HttpGet("webhooks.csv")] public Task<IActionResult> Webhooks(CancellationToken ct)=>FileResult("webhooks","csv",ct);
    [HttpGet("remessas.csv")] public Task<IActionResult> Remessas(CancellationToken ct)=>FileResult("remessas","csv",ct);
    [HttpGet("logs.csv")] public Task<IActionResult> Logs(CancellationToken ct)=>FileResult("logs","csv",ct);
    private async Task<IActionResult> FileResult(string recurso,string formato,CancellationToken ct){var result=await _service.ExportarAsync(recurso,formato,ct).ConfigureAwait(false); if(result.IsFailure||result.Value is null)return BadRequest(ApiResponse<object>.Fail(result.Error??"Exportação inválida.")); return File(result.Value,formato=="json"?"application/json":"text/csv",$"{recurso}.{formato}");}
}
