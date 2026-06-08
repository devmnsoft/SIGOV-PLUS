using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroController : Controller
{
    public IActionResult Dashboard() => View();
    public IActionResult MapaRural() => View();
    public IActionResult CamadasGeo() => View();
}
