using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class OuvidoriaRulesTests
{
    [Fact] public void Ouvidoria_Anonima_Nao_Exige_Pessoa() { var o = new OuvidoriaManifestacao(true, null); o.Anonima.Should().BeTrue(); o.PessoaId.Should().BeNull(); }
    [Fact] public void Ouvidoria_Sigilosa_Oculta_Dados_Pessoais_No_Retorno_Padrao() { var o = new OuvidoriaManifestacao(false, 7); o.DeveOcultarDadosPessoais(false).Should().BeTrue(); }
}
