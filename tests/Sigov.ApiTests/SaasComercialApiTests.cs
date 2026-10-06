using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class SaasComercialApiTests
{
    private static readonly string Controllers = string.Join('\n', Directory.GetFiles(TestRepoPath.Get("src/Sigov.Api/Controllers"), "Saas*.cs").Select(File.ReadAllText));
    [Fact] public void Endpoints_publicos_de_planos_e_cadastro_existentes() { Controllers.Should().Contain("api/publico/planos"); Controllers.Should().Contain("api/publico/cadastro-cliente"); }
    [Fact] public void Endpoints_admin_e_tenant_existentes() { Controllers.Should().Contain("api/saas/solicitacoes-clientes"); Controllers.Should().Contain("api/tenant/minha-assinatura"); Controllers.Should().Contain("api/tenant/branding"); }

    [Fact] public void Rotas_de_administracao_comercial_mnsoft_existentes()
    {
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/assinatura");
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/assinatura/upgrade");
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/assinatura/downgrade");
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/assinatura/cancelar");
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/assinatura/status");
        Controllers.Should().Contain("api/saas/tenants/{tenantId:long}/planos");
    }

    [Fact] public void Mutacoes_comerciais_exigem_permissao_canonica_da_plataforma()
    {
        var comercial = File.ReadAllText(Path.Combine(TestRepoPath.Get("src/Sigov.Api/Controllers"), "SaasTenantComercialController.cs"));
        comercial.Should().Contain("\"saas\", \"plataforma\", \"administrar\"");
        // definição + 4 mutações (criar, upgrade/downgrade compartilhados, cancelar, status)
        comercial.Split("RequireSigovAdminAsync").Length.Should().BeGreaterOrEqualTo(5);
    }
}
