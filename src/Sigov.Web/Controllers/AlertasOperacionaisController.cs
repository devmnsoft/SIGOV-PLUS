using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
public sealed class AlertasOperacionaisController : Controller { [Route("/AlertasOperacionais")] public IActionResult Index() => View(); }
