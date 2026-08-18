using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class AuditoriaController : Controller
{
    public IActionResult Dashboard() => View("Trilhas");
    public IActionResult Eventos() => View("Trilhas");
    public IActionResult Exportacoes() => View("Trilhas");
    public IActionResult FalhasAcesso() => View("Trilhas");
    public IActionResult Trilhas() => View();
    public IActionResult Detalhe(long id = 0) => View(id);
    public IActionResult AcessosDadosPessoais() => View();
    public IActionResult Timeline(string chave = "") => View(model: chave);
}
