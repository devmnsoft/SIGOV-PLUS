using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class LgpdController : Controller
{
    public IActionResult Dashboard() => View("Index");
    public IActionResult Index() => View();
    public IActionResult Consentimentos() => View();
    public IActionResult Solicitacoes() => View();
    public IActionResult SolicitacaoDetalhe(long id = 0) => View(id);
    public IActionResult Incidentes() => View();
    public IActionResult RelatorioTitular() => View();
    public IActionResult ProcessosTratamento() => View();
    public IActionResult RetencaoDescarte() => View();
    public IActionResult AcessosDadosPessoais() => View("~/Views/Auditoria/AcessosDadosPessoais.cshtml");
    public IActionResult Retencao() => View("RetencaoDescarte");
}
