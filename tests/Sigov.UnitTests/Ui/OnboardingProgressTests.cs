using FluentAssertions;
using Sigov.Application.Onboarding;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class OnboardingProgressTests
{
    [Fact]
    public void Onboarding_Deve_Calcular_Progresso_Medio()
    {
        var steps = new[]
        {
            new OnboardingStepDto("organizacao", "Organização", "", 1, OnboardingStatus.Concluido, 100m, []),
            new OnboardingStepDto("usuarios", "Usuários", "", 2, OnboardingStatus.Pendente, 0m, [])
        };

        OnboardingService.CalculateProgress(steps).Should().Be(50m);
    }
}
