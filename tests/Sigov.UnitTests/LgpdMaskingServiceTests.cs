using FluentAssertions;
using Sigov.Infrastructure.Security;
using Xunit;

namespace Sigov.UnitTests;

public sealed class LgpdMaskingServiceTests
{
    [Fact]
    public void Deve_Mascarar_Documentos_Email_E_Telefone()
    {
        var service = new LgpdMaskingService();

        service.Mask("52998224725", "CPF").Should().Be("529******25");
        service.Mask("04252011000110", "CNPJ").Should().Be("04*********110");
        service.Mask("admin@sigov.local", "EMAIL").Should().Be("a***@sigov.local");
        service.Mask("11987654321", "TELEFONE").Should().Be("*******4321");
    }
}
