using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;
namespace Sigov.UnitTests.Agro;
public sealed class AgroPropriedadeRulesTests
{
    [Fact] public void Propriedade_Exige_Produtor() => FluentActions.Invoking(() => new PropriedadeRural(1,1,0,"PR","Nome",1,1,null,null,null,"ATIVA")).Should().Throw<ArgumentException>();
    [Fact] public void Propriedade_Exige_Nome() => FluentActions.Invoking(() => new PropriedadeRural(1,1,1,"PR"," ",1,1,null,null,null,"ATIVA")).Should().Throw<ArgumentException>();
    [Fact] public void Area_Produtiva_Nao_Ultrapassa_Total() => FluentActions.Invoking(() => new PropriedadeRural(1,1,1,"PR","Nome",1,2,null,null,null,"ATIVA")).Should().Throw<ArgumentException>();
}
