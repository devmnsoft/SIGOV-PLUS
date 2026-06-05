using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Processos;

namespace Sigov.Web.Controllers;

public sealed class DiarioOficialController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Criar() => View(new DiarioPublicacaoFormViewModel());
    public IActionResult Detalhe(long id) => View(id);
}
