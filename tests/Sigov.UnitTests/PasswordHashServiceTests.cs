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

        var hash = service.HashPassword("Admin@12345");

        hash.Should().NotContain("Admin@12345");
        service.VerifyPassword("Admin@12345", hash).Should().BeTrue();
        service.VerifyPassword("senha-errada", hash).Should().BeFalse();
    }
}
