using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class SaasComercialTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260608120000_saas_comercial_white_label_planos.sql"));
    [Fact] public void Migration_cria_tabelas_no_schema_sigov_sem_schema_saas() { Migration.Should().Contain("sigov.saas_plano"); Migration.Should().Contain("sigov.saas_tenant_branding"); Migration.Should().NotContain("create schema " + "saas"); }
    [Fact] public void Migration_contem_fluxo_comercial_completo() { Migration.Should().Contain("sigov.saas_solicitacao_cliente"); Migration.Should().Contain("sigov.saas_assinatura"); Migration.Should().Contain("sigov.saas_onboarding_cliente"); Migration.Should().Contain("sigov.saas_perfil_template"); }
    [Fact] public void Migration_contem_isolamento_tenant_operacional() { Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)"); Migration.Should().Contain("unique(tenant_id"); }
}
