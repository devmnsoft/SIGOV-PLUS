using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class SaneamentoModuleSmokeTests
{
    private static string Migration => File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/023_saneamento_base.sql"));

    [Fact]
    public void Migration_Deve_Criar_Tabelas_No_Schema_Sigov_Com_TenantId()
    {
        var sql = Migration;
        sql.Should().Contain("sigov.saneamento_consumidor");
        sql.Should().Contain("sigov.saneamento_evento");
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().NotContain("create schema " + "saneamento");
        sql.Should().NotContain("create schema " + "san");
        sql.Should().NotContain("nvar" + "char");
        sql.Should().NotContain("date" + "time2");
        sql.Should().NotContain("unique" + "identifier");
    }

    [Fact]
    public void Migration_Deve_Ter_Views_Indices_E_Permissoes()
    {
        var sql = Migration;
        sql.Should().Contain("vw_saneamento_dashboard");
        sql.Should().Contain("idx_san_consumidor_tenant_codigo");
        sql.Should().Contain("saneamento.consumidor.visualizar");
        sql.Should().Contain("saneamento.fatura.registrar_pagamento_dev");
    }
}
