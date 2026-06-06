using Xunit;

namespace Sigov.IntegrationTests;

public sealed class IntegracoesModuleSmokeTests
{
    [Fact]
    public void MigrationIntegracoes_UsaSchemaSigovETabelasEsperadas()
    {
        var sql = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "database/postgres/migrations/025_integracoes_outbox_webhooks_base.sql"));
        Assert.Contains("alter table sigov.fila_evento add column if not exists tenant_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("proxima_tentativa_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dead_letter", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create table if not exists sigov.api_credential_scope", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create table if not exists sigov.webhook_enviado", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(sql.Contains("create schema " + "integracao", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("nvar" + "char", StringComparison.OrdinalIgnoreCase));
        Assert.False(sql.Contains("datetime" + "2", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
