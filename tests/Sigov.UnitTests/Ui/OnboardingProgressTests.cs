using FluentAssertions;
using Sigov.Application.Onboarding;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class OnboardingProgressTests
{
    [Fact]
    public void Onboarding_Deve_Calcular_Progresso_Medio()
    {
        var service = new OnboardingService();

        var journey = service.GetJourney(10);

        journey.TenantId.Should().Be(10);
        journey.ProgressPercent.Should().BeGreaterThan(0);
        journey.Steps.Should().HaveCount(12);
    }
}
