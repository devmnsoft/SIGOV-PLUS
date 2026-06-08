using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;
public sealed class AgroParte2ModuleTests
{
    [Fact]
    public void Migration_Agro_Parte2_Deve_Usar_Apenas_Schema_Sigov_E_Tenant()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260608100000_agro_produtores_propriedades_producao.sql"));
        sql.Should().Contain("sigov.agro_produtor").And.Contain("sigov.agro_propriedade").And.Contain("sigov.agro_talhao").And.Contain("sigov.agro_cultura").And.Contain("sigov.agro_safra").And.Contain("sigov.agro_producao_agricola");
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().NotContain("create schema agro").And.NotContain("create schema rural").And.NotContain("create schema geo");
    }
}
