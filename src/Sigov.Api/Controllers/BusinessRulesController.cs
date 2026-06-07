using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.BusinessRules;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/business-rules")]
public sealed class BusinessRulesController : ControllerBase
{
    private readonly IBusinessRuleCatalog _catalog;

    public BusinessRulesController(IBusinessRuleCatalog catalog) => _catalog = catalog;

    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<IBusinessRule>>> Get([FromQuery] string? module)
    {
        var rules = string.IsNullOrWhiteSpace(module) ? _catalog.GetRules() : _catalog.GetRulesByModule(module);
        return Ok(ApiResponse<IReadOnlyList<IBusinessRule>>.Ok(rules));
    }
}
