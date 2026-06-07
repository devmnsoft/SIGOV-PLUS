using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AjudaController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Artigo(string id)
    {
        ViewData["Artigo"] = string.IsNullOrWhiteSpace(id) ? "como-comecar" : id;
        return View();
    }
}
