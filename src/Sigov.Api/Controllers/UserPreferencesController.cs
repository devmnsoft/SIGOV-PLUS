using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Ui;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ui/preferencias")]
[Route("api/ui/preferences")]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _preferenceService;

    public UserPreferencesController(IUserPreferenceService preferenceService) => _preferenceService = preferenceService;

    [HttpGet]
    public ActionResult<ApiResponse<object>> GetAll([FromQuery] long userId, [FromQuery] long? tenantId, [FromQuery] string? key)
    {
        if (userId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Usuário obrigatório para consultar preferências."));
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            return Ok(ApiResponse<object>.Ok(_preferenceService.Get(tenantId, userId, key)));
        }

        var keys = new[] { "tema", "sidebar-recolhida", "pagina-inicial", "tamanho-pagina", "densidade-interface", "modulos-favoritos" };
        var preferences = keys.Select(preferenceKey => _preferenceService.Get(tenantId, userId, preferenceKey)).ToArray();
        return Ok(ApiResponse<object>.Ok(preferences));
    }

    [HttpGet("{chave}")]
    public ActionResult<ApiResponse<UserPreferenceResponse>> GetByKey(string chave, [FromQuery] long userId, [FromQuery] long? tenantId)
    {
        if (userId <= 0)
        {
            return BadRequest(ApiResponse<UserPreferenceResponse>.Fail("Usuário obrigatório para consultar preferências."));
        }

        return Ok(ApiResponse<UserPreferenceResponse>.Ok(_preferenceService.Get(tenantId, userId, chave)));
    }

    [HttpPost]
    public ActionResult<ApiResponse<UserPreferenceResponse>> Save([FromBody] UserPreferenceUpdateRequest request)
    {
        if (request.UserId <= 0)
        {
            return BadRequest(ApiResponse<UserPreferenceResponse>.Fail("Usuário obrigatório para salvar preferências."));
        }

        return Ok(ApiResponse<UserPreferenceResponse>.Ok(_preferenceService.Save(request)));
    }

    [HttpPut("{chave}")]
    public ActionResult<ApiResponse<UserPreferenceResponse>> Put(string chave, [FromBody] UserPreferenceUpdateRequest request)
    {
        if (request.UserId <= 0)
        {
            return BadRequest(ApiResponse<UserPreferenceResponse>.Fail("Usuário obrigatório para salvar preferências."));
        }

        var normalized = request with { Key = chave };
        return Ok(ApiResponse<UserPreferenceResponse>.Ok(_preferenceService.Save(normalized)));
    }
}
