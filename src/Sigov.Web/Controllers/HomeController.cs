using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Login", "Auth");
}
