using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
