using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Processos;

namespace Sigov.Web.Controllers;

public sealed class ProtocolosController : Controller
{
    public IActionResult Index() => View(new ProtocoloFiltroViewModel());
    public IActionResult Criar() => View(new ProtocoloFormViewModel());
    public IActionResult Detalhe(long id) => View(id);
}
