using FluentAssertions;
using Sigov.Domain.Agro.Relatorios;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroIndicadorRulesTests
{
    [Fact] public void Indicador_Exige_Codigo() => FluentActions.Invoking(() => new AgroIndicador(1, null, " ", "Total", AgroIndicadorCategoria.PRODUTORES)).Should().Throw<ArgumentException>();
    [Fact] public void Indicador_Publico_Nao_Expoe_Dado_Pessoal() => FluentActions.Invoking(() => new AgroIndicador(1, null, "cpf", "CPF", AgroIndicadorCategoria.PRODUTORES, true, true)).Should().Throw<ArgumentException>();
}
