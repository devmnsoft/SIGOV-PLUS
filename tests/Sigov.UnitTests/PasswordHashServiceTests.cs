using FluentAssertions;
using Sigov.Infrastructure.Security;
using Xunit;

namespace Sigov.UnitTests;

public sealed class PasswordHashServiceTests
{
    [Fact]
    public void Deve_Gerar_Hash_Verificavel_Sem_Expor_Senha_Pura()
    {
        var service = new PasswordHashService();

        var hash = service.HashPassword("SigovDevLocal!2026");

        hash.Should().NotContain("SigovDevLocal!2026");
        service.VerifyPassword("SigovDevLocal!2026", hash).Should().BeTrue();
        service.VerifyPassword("senha-errada", hash).Should().BeFalse();
    }
}
