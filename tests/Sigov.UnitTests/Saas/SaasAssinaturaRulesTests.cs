using FluentAssertions;
using Sigov.Application.Saas.Comercial;
using Sigov.Domain.Saas.Comercial;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasAssinaturaRulesTests
{
    [Fact] public void Assinatura_exige_tenant_e_plano() => new SaasAssinatura(0, 0, 0, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Usuarios_contratados_deve_ser_maior_que_zero() => new SaasAssinatura(0, 1, 1, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 0, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Dominio_customizado_bloqueia_se_plano_nao_permite() => new SaasAssinatura(0, 1, 1, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 1, false, false).EnsureCustomDomainAllowed(false).IsFailure.Should().BeTrue();

    [Fact] public void Plano_ilimitado_permanece_ilimitado_mesmo_com_addon_de_usuarios()
        => EffectiveUserLimit.SomarBonus(null, 10).Should().BeNull();

    [Fact] public void Bonus_de_addon_de_usuarios_soma_ao_limite_do_plano()
        => EffectiveUserLimit.SomarBonus(20, 10).Should().Be(30);

    [Fact] public void Varios_addons_somam_o_bonus_total()
        => EffectiveUserLimit.SomarBonus(20, 10 + 10).Should().Be(40);

    [Fact] public void Sem_addon_de_usuarios_o_limite_do_plano_nao_altera()
        => EffectiveUserLimit.SomarBonus(20, 0).Should().Be(20);

    [Fact] public void Addon_negativo_ou_nulo_nao_reduz_o_limite_do_plano()
    {
        EffectiveUserLimit.SomarBonus(20, -5).Should().Be(20);
    }
}
