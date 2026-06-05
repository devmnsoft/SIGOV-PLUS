using System.IO;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class SaudeModuleSmokeTests
{
    [Fact]
    public void Migration_saude_acs_base_usa_schema_sigov_e_tenant_id()
    {
        var sql = File.ReadAllText(Path.Combine("..", "..", "..", "..", "database", "postgres", "migrations", "022_saude_acs_base.sql"));
        Assert.Contains("create table if not exists sigov.unidade_saude", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenant_id bigint not null references sigov.tenant(id)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_cria_tabelas_views_indices_e_permissoes_saude()
    {
        var sql = File.ReadAllText(Path.Combine("..", "..", "..", "..", "database", "postgres", "migrations", "022_saude_acs_base.sql"));
        foreach (var table in new[] { "paciente", "atendimento_saude", "farmacia_dispensacao", "vacinacao", "acs_visita", "acs_sync_lote" }) Assert.Contains("sigov." + table, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vw_saude_dashboard", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idx_acs_sync_item_tenant_offline", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("saude.acs.sync", sql, StringComparison.OrdinalIgnoreCase);
    }
}
