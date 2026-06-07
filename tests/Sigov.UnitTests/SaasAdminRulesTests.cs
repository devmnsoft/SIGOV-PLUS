using FluentAssertions;
using Sigov.Application.Saas.Tenants;
using Xunit;

namespace Sigov.UnitTests;

public sealed class SaasAdminRulesTests
{
    [Theory]
    [InlineData("SUSPENSO")]
    [InlineData("CANCELADO")]
    public void Tenant_Suspenso_Ou_Cancelado_Bloqueia_Operacao(string status)
    {
        new TenantValidator().IsOperacaoPermitida(status).Should().BeFalse();
    }
}
