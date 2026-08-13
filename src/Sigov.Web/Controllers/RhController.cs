using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Rh;

namespace Sigov.Web.Controllers;

public sealed class RhController : Controller
{
    public IActionResult Importacoes() => View();
    public IActionResult Pendencias() => View();
    public IActionResult Dashboard() => View();
    public IActionResult Servidores() => View(new RhRegistroViewModel("servidores", "Servidores"));
    public IActionResult ServidorCriar() => View(new RhRegistroViewModel("servidores", "Novo Servidor"));
    public IActionResult ServidorEditar(long id) { ViewData["ServidorId"] = id; return View(new RhRegistroViewModel("servidores", "Editar Servidor")); }
    public IActionResult ServidorDetalhe(long id) { ViewData["ServidorId"] = id; return View(new RhRegistroViewModel("servidores", "Detalhe do Servidor")); }
    public IActionResult Cargos() => View(new RhRegistroViewModel("cargos", "Cargos"));
    public IActionResult Lotacoes() => View(new RhRegistroViewModel("lotacoes", "Lotações"));
    public IActionResult Vinculos() => View(new RhRegistroViewModel("vinculos", "Vínculos"));
    public IActionResult Folhas() => View(new RhRegistroViewModel("folhas", "Folhas de Pagamento"));
    public IActionResult FolhaCriar() => View(new RhRegistroViewModel("folhas", "Nova Folha"));
    public IActionResult FolhaDetalhe(long id) { ViewData["FolhaId"] = id; return View(new RhRegistroViewModel("folhas", "Detalhe da Folha")); }
    public IActionResult FolhaEventos() => View(new RhRegistroViewModel("folha-eventos", "Eventos da Folha"));
    public IActionResult EventosFolha() => RedirectToAction(nameof(FolhaEventos));
    public IActionResult FolhaLancamentos() => View(new RhRegistroViewModel("folha-lancamentos", "Lançamentos da Folha"));
    public IActionResult LancamentosFolha() => RedirectToAction(nameof(FolhaLancamentos));
    [Route("/RH/Ponto")]
    [Route("/RH/Pontos")]
    public IActionResult Pontos() => View(new RhRegistroViewModel("pontos", "Ponto e Frequência"));
    public IActionResult Ferias() => View(new RhRegistroViewModel("ferias", "Férias"));
    public IActionResult Afastamentos() => View(new RhRegistroViewModel("afastamentos", "Afastamentos"));
    public IActionResult SaudeOcupacional() => View(new RhRegistroViewModel("saude-ocupacional", "Saúde Ocupacional"));
    public IActionResult Esocial() => View(new RhRegistroViewModel("esocial", "eSocial Estrutural"));
    public IActionResult Portal() => View();
    public IActionResult PortalContracheques() => View("Portal");
    public IActionResult PortalFerias() => View("Portal");
    public IActionResult PortalAfastamentos() => View("Portal");
    public IActionResult PortalPonto() => View("Portal");
    public IActionResult PortalSolicitacoes() => View("Portal");
    public IActionResult PortalDadosCadastrais() => View("Portal");
}
