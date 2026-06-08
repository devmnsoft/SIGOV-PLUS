using FluentAssertions;
using Sigov.Domain.Saas.Comercial;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasAssinaturaRulesTests
{
    [Fact] public void Assinatura_exige_tenant_e_plano() => new SaasAssinatura(0, 0, 0, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Usuarios_contratados_deve_ser_maior_que_zero() => new SaasAssinatura(0, 1, 1, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 0, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Dominio_customizado_bloqueia_se_plano_nao_permite() => new SaasAssinatura(0, 1, 1, SaasAssinaturaStatus.Ativa, DateOnly.FromDateTime(DateTime.UtcNow), 1, false, false).EnsureCustomDomainAllowed(false).IsFailure.Should().BeTrue();
}
