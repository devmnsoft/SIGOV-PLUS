using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class RhModuleSmokeTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Rh_Migration_Deve_Criar_Tabelas_Minimas_Com_Tenant_E_Soft_Delete()
    {
        var sql = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/020_rh_completo.sql")).ToLowerInvariant();
        foreach (var table in new[] { "servidor", "cargo", "lotacao", "vinculo", "folha", "folha_evento", "folha_lancamento", "ponto", "ferias", "afastamento", "saude_ocupacional", "esocial", "portal_usuario", "portal_acesso", "rh_evento" })
        {
            sql.Should().Contain("'" + table + "'");
        }

        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().Contain("is_deleted boolean not null default false");
        sql.Should().Contain("dados jsonb not null default '{}'::jsonb");
    }

    [Fact]
    public void Rh_Repository_Deve_Usar_Dapper_Parametrizado_E_Filtrar_Tenant()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Rh/RhRepository.cs"));
        code.Should().Contain("TenantId = tenantId");
        code.Should().Contain("where tenant_id = @TenantId");
        code.Should().Contain("Command(");
        code.Should().Contain("cast(@Dados as jsonb)");
    }

    [Fact]
    public void Rh_Api_Deve_Expor_Dashboard_Portal_Exportacao_E_Integracao_Financeira()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/RhController.cs"));
        code.Should().Contain("api/rh");
        code.Should().Contain("dashboard");
        code.Should().Contain("portal/servidores");
        code.Should().Contain("export/{recurso}.{formato}");
        code.Should().Contain("integrar-financeiro");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
