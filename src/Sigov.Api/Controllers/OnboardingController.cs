using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Onboarding;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService) => _onboardingService = onboardingService;

    [HttpGet("{tenantId:long}")]
    public ActionResult<ApiResponse<OnboardingJourneyDto>> Get(long tenantId) => Ok(ApiResponse<OnboardingJourneyDto>.Ok(_onboardingService.GetJourney(tenantId)));
}
