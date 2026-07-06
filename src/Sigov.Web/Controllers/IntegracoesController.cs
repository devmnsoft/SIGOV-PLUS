using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Integracoes;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

public sealed class IntegracoesController : Controller
{
    private readonly IntegracaoMonitorService? _monitor;
    public IntegracoesController() { }
    public IntegracoesController(IntegracaoMonitorService monitor) { _monitor = monitor; }
    [HttpGet("/Integracoes")] public async Task<IActionResult> Index(CancellationToken cancellationToken) => _monitor is null ? Dashboard() : View("~/Views/Operational/Hub.cshtml", await _monitor.GetAsync(cancellationToken));
    [HttpGet("/Integracoes/Conectores")] public async Task<IActionResult> Conectores(CancellationToken cancellationToken) => await Index(cancellationToken);
    [HttpGet("/Integracoes/Reprocessar")] public async Task<IActionResult> Reprocessar(CancellationToken cancellationToken) => await Index(cancellationToken);
    public IActionResult Dashboard() => View(new IntegracaoDashboardViewModel());
    public IActionResult Sistemas() => View(new IntegracaoSistemaFormViewModel());
    public IActionResult ApiCredentials() => View(new ApiCredentialFormViewModel());
    public IActionResult WebhooksRecebidos() => View(new WebhookRecebidoViewModel());
    public IActionResult WebhooksEnviados() => View(new WebhookEnviadoViewModel());
    public IActionResult Outbox() => View(new OutboxEventoViewModel());
    public IActionResult Remessas() => View(new RemessaOficialFormViewModel());
    public IActionResult Certificados() => View(new CertificadoDigitalFormViewModel());
    public IActionResult GovBr() => View(new GovBrConfiguracaoViewModel());
    public IActionResult Assinador() => View();
    [HttpGet("/Integracoes/Logs")] public IActionResult Logs() => View();
    [HttpGet("/Integracoes/Webhooks")]
    [HttpGet("/Integracoes/Webhooks/Novo")]
    [HttpGet("/Integracoes/Webhooks/{id:long}")]
    public IActionResult Webhooks(long? id) => Content($"Webhooks SIGOV: eventos protocolo.criado, protocolo.tramitado, documento.criado, documento.assinado, tarefa.criada, tarefa.concluida, contrato.criado, obra.medicao_registrada, manifestacao.recebida, chamado.aberto, sla.vencido. HMAC/retry/logs preparados; sem envio de dados sensíveis completos. Id={id?.ToString() ?? "lista/novo"}", "text/plain; charset=utf-8");
    [HttpPost("/Integracoes/Webhooks/Novo")]
    [HttpPost("/Integracoes/Webhooks/{id:long}/Testar")]
    [HttpPost("/Integracoes/Webhooks/{id:long}/Desativar")]
    [ValidateAntiForgeryToken]
    public IActionResult WebhooksPost(long? id) { TempData["Warning"] = "Webhook exige URL, secret e schema sigov.webhook_configuracao; nenhum disparo oficial foi simulado."; return Redirect("/Integracoes/Webhooks"); }
}
