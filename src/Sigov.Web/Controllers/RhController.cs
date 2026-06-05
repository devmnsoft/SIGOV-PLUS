using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Rh;

namespace Sigov.Web.Controllers;

public sealed class RhController : Controller
{
    public IActionResult Dashboard() => View();
    public IActionResult Servidores() => View(new RhRegistroViewModel("servidores", "Servidores"));
    public IActionResult Cargos() => View(new RhRegistroViewModel("cargos", "Cargos"));
    public IActionResult Lotacoes() => View(new RhRegistroViewModel("lotacoes", "Lotações"));
    public IActionResult Vinculos() => View(new RhRegistroViewModel("vinculos", "Vínculos"));
    public IActionResult Folhas() => View(new RhRegistroViewModel("folhas", "Folhas de Pagamento"));
    public IActionResult FolhaEventos() => View(new RhRegistroViewModel("folha-eventos", "Eventos da Folha"));
    public IActionResult FolhaLancamentos() => View(new RhRegistroViewModel("folha-lancamentos", "Lançamentos da Folha"));
    public IActionResult Pontos() => View(new RhRegistroViewModel("pontos", "Ponto e Frequência"));
    public IActionResult Ferias() => View(new RhRegistroViewModel("ferias", "Férias"));
    public IActionResult Afastamentos() => View(new RhRegistroViewModel("afastamentos", "Afastamentos"));
    public IActionResult SaudeOcupacional() => View(new RhRegistroViewModel("saude-ocupacional", "Saúde Ocupacional"));
    public IActionResult Esocial() => View(new RhRegistroViewModel("esocial", "eSocial Estrutural"));
    public IActionResult Portal() => View();
}
