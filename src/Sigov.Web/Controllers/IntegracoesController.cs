using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Integracoes;

namespace Sigov.Web.Controllers;

public sealed class IntegracoesController : Controller
{
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
    public IActionResult Logs() => View();
}
