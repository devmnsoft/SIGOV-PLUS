using FluentAssertions;
using Sigov.Domain.Saas.WhiteLabel;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class TenantBrandingRulesTests
{
    [Fact] public void White_label_bloqueia_se_plano_nao_permite() => new TenantBranding(0, 1, "Tenant", true, WhiteLabelTema.Sigov, null).Validate(false).IsFailure.Should().BeTrue();
    [Fact] public void Css_customizado_e_limitado_e_sanitizado() => TenantBranding.SanitizeCss("<script>").Should().NotContain("<");
}
