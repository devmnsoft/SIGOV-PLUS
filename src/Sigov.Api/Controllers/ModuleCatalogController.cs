using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Commercial;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ui/module-catalog")]
public sealed class ModuleCatalogController : ControllerBase
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public ModuleCatalogController(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<ModuleCatalogItem>>> Get() => Ok(ApiResponse<IReadOnlyList<ModuleCatalogItem>>.Ok(_moduleCatalogService.GetModules()));
}
