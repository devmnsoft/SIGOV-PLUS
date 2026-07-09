using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Onboarding;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Route("api/ui/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService) => _onboardingService = onboardingService;

    [HttpGet]
    public ActionResult<ApiResponse<OnboardingJourneyDto>> GetDefault([FromQuery] long? tenantId)
    {
        if (!tenantId.HasValue || tenantId.Value <= 0) return BadRequest(ApiResponse<OnboardingJourneyDto>.Fail("Informe tenantId para consultar onboarding."));
        return Ok(ApiResponse<OnboardingJourneyDto>.Ok(_onboardingService.GetJourney(tenantId.Value)));
    }

    [HttpGet("{tenantId:long}")]
    public ActionResult<ApiResponse<OnboardingJourneyDto>> Get(long tenantId) => Ok(ApiResponse<OnboardingJourneyDto>.Ok(_onboardingService.GetJourney(tenantId)));
}
