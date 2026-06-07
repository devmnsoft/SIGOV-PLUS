using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Ui;

namespace Sigov.Web.Controllers;

public sealed class PreferenciasController : Controller
{
    private readonly IUserPreferenceService _preferenceService;

    public PreferenciasController(IUserPreferenceService preferenceService) => _preferenceService = preferenceService;

    public IActionResult Index() => View(_preferenceService.Get(1, 1, "tema"));
}
