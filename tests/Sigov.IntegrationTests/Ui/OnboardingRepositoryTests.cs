using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Ui;

public sealed class OnboardingRepositoryTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Repositorio_Deve_Listar_Tarefas_Por_Tenant_E_Jornada()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Onboarding", "OnboardingRepository.cs"));

        source.Should().Contain("sigov.onboarding_tarefa");
        source.Should().Contain("tenant_id = @TenantId");
        source.Should().Contain("jornada_id = @JourneyId");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
