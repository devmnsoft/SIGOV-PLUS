using Microsoft.AspNetCore.Mvc;

namespace SIGOV.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
