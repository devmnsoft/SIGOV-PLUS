using FluentAssertions;
using Sigov.Domain.Saas.Comercial;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasPlanoRulesTests
{
    [Fact] public void Plano_exige_codigo_e_nome() => new SaasPlano(0, "", "", "descrição", true, SaasPlanoTipo.Publico, 0, SaasPeriodicidade.Mensal, 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Preco_negativo_falha() => new SaasPlano(0, "ESS", "Essencial", "descrição", true, SaasPlanoTipo.Publico, -1, SaasPeriodicidade.Mensal, 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Limite_de_usuarios_negativo_falha() => new SaasPlano(0, "ESS", "Essencial", "descrição", true, SaasPlanoTipo.Publico, 1, SaasPeriodicidade.Mensal, -1, false, false).Validate().IsFailure.Should().BeTrue();
}
