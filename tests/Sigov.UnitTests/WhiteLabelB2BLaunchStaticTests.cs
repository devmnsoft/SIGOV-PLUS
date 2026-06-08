using FluentAssertions;

namespace Sigov.UnitTests;

public sealed class WhiteLabelB2BLaunchStaticTests
{
    [Fact]
    public void Migration_Deve_Conter_Estruturas_Criticas_Do_Lancamento_B2B()
    {
        var path = TestRepoPath.Get("database/postgres/migrations/20260608120000_plantao_pro_white_label_b2b_launch.sql");
        var sql = File.ReadAllText(path);

        sql.Should().Contain("create schema if not exists plantaopro");
        sql.Should().Contain("create table if not exists sigov.b2b_planos");
        sql.Should().Contain("create table if not exists sigov.b2b_tenant_white_label");
        sql.Should().Contain("create table if not exists sigov.b2b_api_chaves");
        sql.Should().Contain("create table if not exists sigov.b2b_contratos");
        sql.Should().Contain("create table if not exists sigov.b2b_suporte_chamados");
        sql.Should().Contain("create table if not exists sigov.b2b_telemetria_alertas");
        sql.Should().Contain("DO $$");
        sql.Should().NotContain("ADD CONSTRAINT IF NOT EXISTS");
    }

    [Fact]
    public void Controller_Deve_Expor_Rotas_B2B_Criticas()
    {
        var path = TestRepoPath.Get("src/Sigov.Api/Controllers/Saas/WhiteLabelB2BLaunchController.cs");
        var code = File.ReadAllText(path);

        code.Should().Contain("api/planos/publicos");
        code.Should().Contain("api/self-service/cadastro");
        code.Should().Contain("api/white-label/tenant/{tenantId:long}");
        code.Should().Contain("api/developer/api-keys");
        code.Should().Contain("api/minha-assinatura/solicitar-upgrade");
        code.Should().Contain("api/contratos");
        code.Should().Contain("api/suporte/chamados");
        code.Should().Contain("api/monitoramento/alertas");
        code.Should().Contain("ApiResponse<");
    }
}
