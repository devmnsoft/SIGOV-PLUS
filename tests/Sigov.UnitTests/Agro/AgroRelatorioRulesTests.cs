using FluentAssertions;
using Sigov.Domain.Agro.Relatorios;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroRelatorioRulesTests
{
    [Fact] public void Relatorio_Exige_Nome() => FluentActions.Invoking(() => new AgroRelatorioModelo(1, null, "prod", " ", AgroRelatorioTipo.PRODUTORES)).Should().Throw<ArgumentException>();
    [Fact] public void Relatorio_Com_Dado_Pessoal_Eh_Privado_Por_Padrao() => new AgroRelatorioModelo(1, null, "prod", "Produtores", AgroRelatorioTipo.PRODUTORES, AgroRelatorioFormato.HTML, true, true).PublicoNoTenant.Should().BeFalse();
    [Fact] public void Execucao_Exige_Formato() => FluentActions.Invoking(() => new AgroRelatorioExecucao(1, null)).Should().Throw<ArgumentNullException>();
}
