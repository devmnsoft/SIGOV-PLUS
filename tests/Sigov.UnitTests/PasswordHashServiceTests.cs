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

    [Theory]
    [InlineData("SigovDevLocal!2026", "SIGOV_PBKDF2_V1$210000$U0lHT1ZfREVWX1NBTFQhIQ==$kKnj2QPLDyk92OudwUguJk6BJV8qHTDJTvWv+v9JLxQ=")]
    [InlineData("SigovSuperAdmin!2026", "SIGOV_PBKDF2_V1$210000$U0lHT1ZfU1VQRVJfU0FMVA==$55mXRMqQ4e9CW6f4f2qCvH/Ony2irtPRb4S7SjfeqFI=")]
    public void Deve_Validar_Hashes_Fixos_Do_Guard_Development(string password, string hash)
    {
        new PasswordHashService().VerifyPassword(password, hash).Should().BeTrue();
    }

    [Theory]
    [InlineData("senha-em-texto-puro")]
    [InlineData("SIGOV_PBKDF2_V1$100000$base64-invalido$invalido")]
    [InlineData("SIGOV_PBKDF2_V1$1$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Deve_Rejeitar_Hash_Legado_Malformado_Sem_Lancar_Excecao(string hash)
    {
        var service = new PasswordHashService();
        service.VerifyPassword("SenhaForte!2026", hash).Should().BeFalse();
    }
}
