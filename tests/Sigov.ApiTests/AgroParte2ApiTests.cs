using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;
public sealed class AgroParte2ApiTests
{
    [Fact]
    public void Controllers_Agro_Parte2_Deve_Expor_Rotas_REST()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/AgroProdutoresController.cs")).Should().Contain("api/agro/produtores");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/AgroPropriedadesController.cs")).Should().Contain("api/agro/propriedades").And.Contain("api/agro/talhoes").And.Contain("api/agro/culturas").And.Contain("api/agro/safras");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/AgroProducaoController.cs")).Should().Contain("api/agro/producao");
    }
}
