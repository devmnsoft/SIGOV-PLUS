using FluentAssertions;
using Xunit;

namespace Sigov.UnitTests.Release;

public sealed class SmokeTestScriptTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void SmokeScript_Deve_Usar_SigovSmokePassword()
    {
        var script = File.ReadAllText(Path.Combine(Root, "scripts", "smoke-test.ps1"));

        script.Should().Contain("SIGOV_SMOKE_PASSWORD");
        script.Should().Contain("/api/health/version");
        script.Should().NotContain("sigov_admin_password");
    }


    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
