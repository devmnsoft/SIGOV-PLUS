using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AuditoriaController : Controller
{
    public IActionResult Trilhas() => View();
    public IActionResult Detalhe(long id = 0) => View(id);
    public IActionResult AcessosDadosPessoais() => View();
    public IActionResult Timeline(string chave = "") => View(model: chave);
}
