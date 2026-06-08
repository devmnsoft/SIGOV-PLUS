using FluentAssertions;
using Sigov.Domain.Agro.Comercial;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroPainelComercialRulesTests
{
    [Fact] public void Painel_Comercial_Respeita_Tenant() => FluentActions.Invoking(() => new AgroPainelComercialConfig(0, null, "Agro")).Should().Throw<ArgumentException>();
    [Fact] public void Painel_Comercial_Aceita_Tenant_Valido() => new AgroPainelComercialConfig(1, null, "Agro").TenantId.Should().Be(1);
}
