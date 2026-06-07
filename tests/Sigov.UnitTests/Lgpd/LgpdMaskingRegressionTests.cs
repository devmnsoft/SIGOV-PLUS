using FluentAssertions;
using Sigov.Infrastructure.Lgpd;
using Sigov.Infrastructure.Security;
using Xunit;

namespace Sigov.UnitTests.Lgpd;

public sealed class LgpdMaskingRegressionTests
{
    private readonly LgpdMaskingPolicy _policy = new(new LgpdClassificationService(), new LgpdMaskingService());

    [Theory]
    [InlineData("12345678901", "cpf", "123******01")]
    [InlineData("12345678000199", "cnpj", "12*********199")]
    [InlineData("cidadao@sigov.local", "email", "c***@sigov.local")]
    [InlineData("11987654321", "telefone", "*******4321")]
    [InlineData("12345678901", "nis", "***")]
    [InlineData("123456789012345", "cartao_sus", "***")]
    [InlineData("token-claro", "token", "***")]
    [InlineData("api-key-clara", "api_key", "***")]
    [InlineData("001/12345", "dados_bancarios", "***")]
    [InlineData("diagnostico", "prontuario", "***")]
    [InlineData("parecer social sensivel", "parecer_social", "***")]
    public void Deve_Mascarar_Dados_Pessoais_Sensiveis_E_Secrets_Por_Padrao(string value, string fieldName, string expected)
    {
        _policy.Mask(value, fieldName).Should().Be(expected);
    }

    [Fact]
    public void Campo_Nao_Classificado_Deve_Retornar_Valor_Sem_Mascara()
    {
        _policy.Mask("valor publico", "descricao_publica").Should().Be("valor publico");
    }
}
