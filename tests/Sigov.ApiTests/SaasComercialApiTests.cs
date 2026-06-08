using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class SaasComercialApiTests
{
    private static readonly string Controllers = string.Join('\n', Directory.GetFiles(TestRepoPath.Get("src/Sigov.Api/Controllers"), "Saas*.cs").Select(File.ReadAllText));
    [Fact] public void Endpoints_publicos_de_planos_e_cadastro_existentes() { Controllers.Should().Contain("api/publico/planos"); Controllers.Should().Contain("api/publico/cadastro-cliente"); }
    [Fact] public void Endpoints_admin_e_tenant_existentes() { Controllers.Should().Contain("api/saas/solicitacoes-clientes"); Controllers.Should().Contain("api/tenant/minha-assinatura"); Controllers.Should().Contain("api/tenant/branding"); }
}
