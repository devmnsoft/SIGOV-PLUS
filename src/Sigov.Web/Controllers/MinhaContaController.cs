using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Account;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MinhaContaController : Controller
{
    [HttpGet("/Perfil")]
    [HttpGet("/MinhaConta")]
    public IActionResult Index()
    {
        var model = new MyAccountViewModel(
            User.FindFirstValue(ClaimTypes.Name) ?? "Não informado",
            User.FindFirstValue("login") ?? "Não informado",
            User.FindFirstValue(ClaimTypes.Email) ?? "Não informado",
            User.FindFirstValue("tenant_name") ?? User.FindFirstValue("tenant_id") ?? "Não informado",
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            User.Identity?.IsAuthenticated == true);
        return View(model);
    }
}
