using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Onboarding;

namespace Sigov.Web.Controllers;

public sealed class OnboardingController : Controller
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService) => _onboardingService = onboardingService;

    public IActionResult Index() => View(_onboardingService.GetJourney(1));

    public IActionResult Detalhe(long id) => View(_onboardingService.GetJourney(id <= 0 ? 1 : id));
}
