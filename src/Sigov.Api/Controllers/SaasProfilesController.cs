using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Profiles;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas")]
public sealed class SaasProfilesController : ControllerBase
{
    private readonly IEffectivePermissionService _effectivePermissionService;
    private readonly IProfileLevelService _profileLevelService;

    public SaasProfilesController(IProfileLevelService profileLevelService, IEffectivePermissionService effectivePermissionService)
    {
        _profileLevelService = profileLevelService;
        _effectivePermissionService = effectivePermissionService;
    }

    [HttpGet("perfis/niveis")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProfileLevelItem>>>> GetLevels(CancellationToken cancellationToken)
    {
        var levels = await _profileLevelService.GetLevelsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<ProfileLevelItem>>.Ok(levels));
    }

    [HttpGet("usuarios/{usuarioId:long}/permissoes-efetivas")]
    public async Task<ActionResult<ApiResponse<EffectivePermissionResult>>> GetEffectivePermissions(long usuarioId, [FromQuery] long? tenantId, CancellationToken cancellationToken)
    {
        var result = await _effectivePermissionService.CalculateAsync(usuarioId, tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<EffectivePermissionResult>.Ok(result));
    }
}
