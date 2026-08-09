using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Web.Models.Account;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MinhaContaController : Controller
{
    private readonly ICurrentUser _currentUser;

    public MinhaContaController(ICurrentUser currentUser) => _currentUser = currentUser;

    [HttpGet("/Perfil")]
    [HttpGet("/MinhaConta")]
    public IActionResult Index()
    {
        var model = new MyAccountViewModel(
            _currentUser.Nome ?? "Não informado",
            _currentUser.Login ?? "Não informado",
            _currentUser.Email ?? "Não informado",
            _currentUser.TenantName ?? _currentUser.TenantId?.ToString() ?? "Não informado",
            _currentUser.Roles,
            _currentUser.Permissions,
            _currentUser.IsAuthenticated);
        return View(model);
    }
}
