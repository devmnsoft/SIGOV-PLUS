using FluentAssertions;
using Sigov.Domain.Core;
using Xunit;

namespace Sigov.UnitTests.Core;

public sealed class PessoaRulesTests
{
    [Fact]
    public void Pessoa_Fisica_Normaliza_Cpf()
    {
        var pessoa = new Pessoa(TipoPessoa.Fisica, " Maria ", "000.000.001-91");

        pessoa.Nome.Should().Be("Maria");
        pessoa.Documento.Should().Be("00000000191");
    }

    [Fact]
    public void Pessoa_Juridica_Exige_Cnpj_Com_14_Digitos()
    {
        var act = () => new Pessoa(TipoPessoa.Juridica, "Fornecedor", "123");

        act.Should().Throw<ArgumentException>().WithMessage("CNPJ deve conter 14 dígitos.*");
    }

    [Fact]
    public void Pessoa_Exige_Nome()
    {
        var act = () => new Pessoa(TipoPessoa.Fisica, " ", null);

        act.Should().Throw<ArgumentException>().WithMessage("Nome é obrigatório.*");
    }
}
