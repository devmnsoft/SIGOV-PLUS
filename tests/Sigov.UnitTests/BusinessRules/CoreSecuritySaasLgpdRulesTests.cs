using FluentAssertions;
using Sigov.Application.BusinessRules;
using Xunit;

namespace Sigov.UnitTests.BusinessRules;

public sealed class CoreSecuritySaasLgpdRulesTests
{
    [Theory]
    [InlineData("Core/Pessoas", "Pessoa exige nome")]
    [InlineData("Core/Cadastros", "Exercício encerrado bloqueia")]
    [InlineData("Segurança", "Não bloquear o único administrador ativo")]
    [InlineData("SaaS", "Somente SIGOV_ADMIN acessa SaaS Admin")]
    [InlineData("Auditoria", "Alteração guarda antes/depois")]
    [InlineData("LGPD", "Consentimento revogado não pode ser usado como base ativa")]
    public void Catalogo_Deve_Conter_Regras_Do_Lote_1(string modulo, string trecho)
    {
        var catalog = new BusinessRuleCatalog();

        catalog.GetRulesByModule(modulo).Should().Contain(rule => rule.Description.Contains(trecho, StringComparison.Ordinal));
    }
}
