using FluentAssertions;
using Sigov.Application.Lgpd;
using Xunit;

namespace Sigov.UnitTests;

public sealed class LgpdRulesTests
{
    [Fact]
    public void Consentimento_Revogado_Nao_E_Base_Ativa()
    {
        new ConsentimentoService().IsBaseAtiva(revogado: true).Should().BeFalse();
    }
}
