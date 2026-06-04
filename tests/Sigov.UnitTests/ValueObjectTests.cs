using FluentAssertions;
using Sigov.Domain.Common;
using Xunit;

namespace Sigov.UnitTests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Cpf_Deve_Normalizar_E_Validar_Digitos()
    {
        var cpf = Cpf.Create("529.982.247-25");

        cpf.IsSuccess.Should().BeTrue();
        cpf.Value!.Value.Should().Be("52998224725");
        Cpf.Create("111.111.111-11").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cnpj_Deve_Normalizar_E_Validar_Digitos()
    {
        var cnpj = Cnpj.Create("04.252.011/0001-10");

        cnpj.IsSuccess.Should().BeTrue();
        cnpj.Value!.Value.Should().Be("04252011000110");
        Cnpj.Create("00.000.000/0000-00").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CpfCnpj_Deve_Aceitar_Apenas_Documento_Valido()
    {
        CpfCnpj.Create("529.982.247-25").Value!.Tipo.Should().Be("CPF");
        CpfCnpj.Create("04.252.011/0001-10").Value!.Tipo.Should().Be("CNPJ");
        CpfCnpj.Create("123").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Email_Deve_Normalizar_Endereco()
    {
        var email = Email.Create(" ADMIN@SIGOV.LOCAL ");

        email.IsSuccess.Should().BeTrue();
        email.Value!.Value.Should().Be("admin@sigov.local");
        Email.Create("invalido").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Telefone_E_Cep_Devem_Validar_Digitos()
    {
        Telefone.Create("(11) 98765-4321").Value!.Value.Should().Be("11987654321");
        Telefone.Create("11111111111").IsFailure.Should().BeTrue();
        Cep.Create("01310-100").Value!.Value.Should().Be("01310100");
        Cep.Create("00000-000").IsFailure.Should().BeTrue();
    }
}
