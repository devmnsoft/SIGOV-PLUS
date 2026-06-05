using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Processos;

namespace Sigov.Web.Controllers;

public sealed class OuvidoriaController : Controller
{
    public IActionResult Index() => View(new OuvidoriaFiltroViewModel());
    public IActionResult Criar() => View(new OuvidoriaFormViewModel());
    public IActionResult Detalhe(long id) => View(id);
}
