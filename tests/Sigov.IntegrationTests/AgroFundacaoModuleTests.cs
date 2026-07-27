using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class AgroFundacaoModuleTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/026_agro_fundacao_geo_dashboard.sql"));

    [Fact]
    public void Migration_Deve_Criar_Tabelas_E_View_No_Schema_Sigov()
    {
        Migration.Should().Contain("create table if not exists sigov.agro_geo_camada");
        Migration.Should().Contain("create table if not exists sigov.agro_geo_feicao");
        Migration.Should().Contain("create table if not exists sigov.agro_evento");
        Migration.Should().Contain("create or replace view sigov.vw_agro_dashboard");
        Migration.Should().NotContain("create schema " + "agro");
        Migration.Should().NotContain(" agro" + ".");
    }

    [Fact]
    public void Migration_Deve_Garantir_TenantId_Indices_Permissoes_E_Catalogo()
    {
        Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        Migration.Should().Contain("idx_agro_geo_camada_tenant_codigo");
        Migration.Should().Contain("idx_agro_geo_feicao_tenant_camada");
        Migration.Should().Contain("idx_agro_geo_feicao_tenant_origem");
        Migration.Should().Contain("idx_agro_evento_tenant_tipo");
        Migration.Should().Contain("idx_agro_evento_created_at");
        Migration.Should().Contain("agro.dashboard.visualizar");
        Migration.Should().Contain("agro.geo.exportar");
        Migration.Should().Contain("agro.exportacao_geojson");
    }

    [Fact]
    public void Migration_Deve_Ter_Isolamento_Tenant_Em_Estruturas_Operacionais()
    {
        Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        Migration.Should().Contain("unique (tenant_id, entidade_id, codigo)");
        Migration.Should().Contain("camada_id bigint not null references sigov.agro_geo_camada(id)");
        Migration.Should().Contain("exercicio_id bigint null references sigov.exercicio(id)");
    }
}
