using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class DesignSystemController : Controller
{
    private readonly IWebHostEnvironment _environment;
    private readonly IUserPermissionService _permissions;

    public DesignSystemController(IWebHostEnvironment environment, IUserPermissionService permissions)
    {
        _environment = environment;
        _permissions = permissions;
    }

    [HttpGet("/DesignSystem")]
    public IActionResult Index()
    {
        if (!_environment.IsDevelopment() && !_permissions.HasPermission(User, "ADMIN_GERAL"))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();

        Response.Headers.CacheControl = "no-store";
        return View();
    }
}
