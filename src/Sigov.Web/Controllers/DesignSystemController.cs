using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class DesignSystemController : Controller
{
    public IActionResult Index() => View();
}
