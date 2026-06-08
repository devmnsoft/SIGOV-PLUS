using FluentAssertions;
using Sigov.Domain.Agro.Dicionario;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroDicionarioDadosRulesTests
{
    [Fact] public void Campo_Pessoal_No_Dicionario_Deve_Ter_Mascara() => FluentActions.Invoking(() => new AgroDicionarioDados("agro_produtor", "cpf", true, false, null)).Should().Throw<ArgumentException>();
}
