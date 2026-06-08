using FluentAssertions;
using Sigov.Domain.Agro;
using Xunit;
namespace Sigov.UnitTests.Agro;
public sealed class AgroProducaoRulesTests
{
    [Fact] public void Producao_Exige_Produtor_E_Cultura(){ FluentActions.Invoking(() => new ProducaoAgricola(1,1,null,0,1,1,10,null,null,null,"kg","PLANEJADA")).Should().Throw<ArgumentException>(); FluentActions.Invoking(() => new ProducaoAgricola(1,1,null,1,0,1,10,null,null,null,"kg","PLANEJADA")).Should().Throw<ArgumentException>(); }
    [Fact] public void Producao_Nao_Aceita_Area_Negativa() => FluentActions.Invoking(() => new ProducaoAgricola(1,1,null,1,1,-1,10,null,null,null,"kg","PLANEJADA")).Should().Throw<ArgumentException>();
    [Fact] public void Produtividade_E_Calculada_Quando_Possivel(){ var p = new ProducaoAgricola(1,1,null,1,1,2,10,null,null,null,"kg","PLANEJADA"); p.Produtividade.Should().Be(5); }
}
