using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Api.Contracts;
using Sigov.Application.Onboarding;
using Sigov.Application.Abstractions;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/onboarding")]
[Route("api/ui/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentTenant _currentTenant;

    public OnboardingController(IOnboardingService onboardingService, ICurrentTenant currentTenant)
        => (_onboardingService, _currentTenant) = (onboardingService, currentTenant);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OnboardingJourneyDto>>> GetDefault([FromQuery] long? tenantId, CancellationToken cancellationToken)
    {
        var current = _currentTenant.TenantId;
        if (!current.HasValue || current <= 0) return Forbid();
        if (tenantId.HasValue && tenantId != current) return Forbid();
        var journey = await _onboardingService.GetJourneyAsync(current.Value, cancellationToken).ConfigureAwait(false);
        return journey is null ? NotFound(ApiResponse<OnboardingJourneyDto>.Fail("Implantação não configurada para o contexto atual.")) : Ok(ApiResponse<OnboardingJourneyDto>.Ok(journey));
    }

    [HttpGet("{tenantId:long}")]
    public Task<ActionResult<ApiResponse<OnboardingJourneyDto>>> Get(long tenantId, CancellationToken cancellationToken)
        => GetDefault(tenantId, cancellationToken);
}
