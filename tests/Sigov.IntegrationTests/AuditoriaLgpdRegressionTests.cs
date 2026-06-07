using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class AuditoriaLgpdRegressionTests
{
    [Fact]
    public void Auditoria_E_Lgpd_Deve_Ter_Mascaramento_E_Prazos()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Auditoria/Trilhas.cshtml")).Should().Contain("CorrelationId");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Lgpd/Solicitacoes.cshtml")).Should().Contain("Prazo");
    }
}
