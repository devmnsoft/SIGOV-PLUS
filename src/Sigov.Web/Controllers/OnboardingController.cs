using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Application.Onboarding;
using Sigov.Application.Abstractions;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class OnboardingController : Controller
{
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentTenant _currentTenant;

    public OnboardingController(IOnboardingService onboardingService, ICurrentTenant currentTenant)
        => (_onboardingService, _currentTenant) = (onboardingService, currentTenant);

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue || tenantId <= 0) return Forbid();
        var journey = await _onboardingService.GetJourneyAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return journey is null ? NotFound() : View(journey);
    }

    public async Task<IActionResult> Detalhe(long id, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue || tenantId <= 0 || id != tenantId.Value) return Forbid();
        var journey = await _onboardingService.GetJourneyAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return journey is null ? NotFound() : View(journey);
    }
}
