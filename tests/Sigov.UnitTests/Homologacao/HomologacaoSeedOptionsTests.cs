using FluentAssertions;
using Sigov.Application.Homologacao;
using Xunit;

namespace Sigov.UnitTests.Homologacao;

public sealed class HomologacaoSeedOptionsTests
{
    [Fact]
    public void Validator_Deve_Bloquear_Production()
    {
        var validator = new HomologacaoValidator();

        Action action = () => validator.EnsureCanRun("Production");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Options_Deve_Ter_Defaults_Seguros()
    {
        var options = new HomologacaoSeedOptions("homologacao", "admin.hml@sigov.local", null, false);

        options.TenantSlug.Should().Be("homologacao");
        options.EnableDemoData.Should().BeFalse();
    }
}
