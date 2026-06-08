using FluentAssertions;
using Sigov.Domain.Saas.Perfis;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasPerfilTemplateRulesTests
{
    [Fact] public void Perfil_template_exige_nivel_base() => new SaasPerfilTemplate(0, "TMP", "Template", "").Validate().IsFailure.Should().BeTrue();
    [Fact] public void Perfil_local_nao_vira_administrador_geral() => new SaasPerfilTemplate(0, "TMP", "Template", "ADMINISTRADOR_GERAL").Validate().IsFailure.Should().BeTrue();
}
