using FluentAssertions;
using Sigov.Application.BusinessRules;
using Xunit;

namespace Sigov.UnitTests.BusinessRules;

public sealed class BusinessRuleCatalogTests
{
    [Fact]
    public void Catalogo_Deve_Expor_Regras_Por_Modulo()
    {
        var catalog = new BusinessRuleCatalog();

        catalog.GetRulesByModule("SaaS").Should().Contain(rule => rule.Description.Contains("Tenant não pode acessar dados de outro tenant", StringComparison.Ordinal));
        catalog.GetRulesByModule("Financeiro").Should().Contain(rule => rule.Description.Contains("Empenho", StringComparison.Ordinal));
        catalog.GetRules().Should().HaveCountGreaterThan(70);
    }
}
