using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class PosRcHomologacaoMigrationTests
{
    private static readonly string MigrationPath = Path.Combine(TestRepoPath.Root, "database", "postgres", "migrations", "20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql");

    [Fact]
    public void Migration_PosRc_Deve_Ser_Idempotente_E_TenantAware()
    {
        var sql = File.ReadAllText(MigrationPath);

        sql.Should().Contain("create table if not exists sigov.api_key");
        sql.Should().Contain("create table if not exists sigov.outbox_evento");
        sql.Should().Contain("create table if not exists sigov.protocolo");
        sql.Should().Contain("create table if not exists sigov.documento");
        sql.Should().Contain("create table if not exists sigov.workflow_instancia");
        sql.Should().Contain("tenant_id bigint not null");
        sql.Should().Contain("is_deleted boolean not null default false");
        sql.Should().Contain("correlation_id uuid null");
        sql.Should().Contain("create index if not exists");
    }

    [Fact]
    public void Migration_PosRc_Nao_Deve_Armazenar_Token_Claro()
    {
        var sql = File.ReadAllText(MigrationPath);

        sql.Should().Contain("api_key_hash");
        sql.Should().Contain("prefixo");
        sql.Should().NotContain("api_key_texto_claro");
        sql.Should().NotContain("token_claro");
    }
}
