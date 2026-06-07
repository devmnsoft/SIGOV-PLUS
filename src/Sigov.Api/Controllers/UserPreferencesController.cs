using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Ui;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ui/preferences")]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _preferenceService;

    public UserPreferencesController(IUserPreferenceService preferenceService) => _preferenceService = preferenceService;

    [HttpGet]
    public ActionResult<ApiResponse<UserPreferenceResponse>> Get([FromQuery] long userId, [FromQuery] string key, [FromQuery] long? tenantId) => Ok(ApiResponse<UserPreferenceResponse>.Ok(_preferenceService.Get(tenantId, userId, key)));

    [HttpPost]
    public ActionResult<ApiResponse<UserPreferenceResponse>> Save([FromBody] UserPreferenceUpdateRequest request) => Ok(ApiResponse<UserPreferenceResponse>.Ok(_preferenceService.Save(request)));
}
