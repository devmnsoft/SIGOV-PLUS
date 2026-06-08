using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroProdutorRulesTests
{
    [Fact] public void Produtor_Exige_Pessoa() => FluentActions.Invoking(() => new ProdutorRural(1,1,0,"P1","FAMILIAR","ATIVO")).Should().Throw<ArgumentException>();
    [Fact] public void Produtor_Exige_Codigo() => FluentActions.Invoking(() => new ProdutorRural(1,1,1," ","FAMILIAR","ATIVO")).Should().Throw<ArgumentException>();
}
