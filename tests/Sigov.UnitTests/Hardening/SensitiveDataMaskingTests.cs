using FluentAssertions;
using Sigov.Infrastructure.Security;
using Xunit;

namespace Sigov.UnitTests.Hardening;

public sealed class SensitiveDataMaskingTests
{
    private readonly LgpdMaskingService _service = new();

    [Theory]
    [InlineData("12345678901", "CPF", "123******01")]
    [InlineData("12345678000199", "CNPJ", "12*********199")]
    [InlineData("user@example.gov.br", "EMAIL", "u***@example.gov.br")]
    [InlineData("11987654321", "TELEFONE", "*******4321")]
    [InlineData("token-claro", "TOKEN", "***")]
    [InlineData("dados de saúde", "SAUDE", "***")]
    public void Deve_Mascarar_Dados_Pessoais_Sensiveis_E_Secrets(string value, string dataType, string expected)
    {
        _service.Mask(value, dataType).Should().Be(expected);
    }
}
