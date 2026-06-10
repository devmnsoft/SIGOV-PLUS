using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Processos;

namespace Sigov.Web.Controllers;

public sealed class ProcessosController : Controller
{
    public IActionResult Index() => View(new ProcessoDigitalFiltroViewModel());
    public IActionResult Criar() => View(new ProcessoDigitalFormViewModel());
    public IActionResult Editar(long id) => View(new ProcessoDigitalFormViewModel { Id = id });
    public IActionResult Detalhe(long id) => View(id);
    public IActionResult Ged() => RedirectToAction("Dashboard", "Ged");
}
