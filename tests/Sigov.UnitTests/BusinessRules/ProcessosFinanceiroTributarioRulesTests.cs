using FluentAssertions;
using Sigov.Application.BusinessRules;
using Xunit;

namespace Sigov.UnitTests.BusinessRules;

public sealed class ProcessosFinanceiroTributarioRulesTests
{
    [Theory]
    [InlineData("Processos", "Processo cancelado não movimenta")]
    [InlineData("Processos", "Parecer exige texto")]
    [InlineData("Financeiro", "Empenho não pode ultrapassar saldo disponível")]
    [InlineData("Financeiro", "Dinheiro usa decimal")]
    [InlineData("Tributário", "DAM fake bloqueado em Production")]
    [InlineData("Tributário", "Carnê finaliza apenas com itens")]
    public void Catalogo_Deve_Conter_Regras_Do_Lote_2(string modulo, string trecho)
    {
        var catalog = new BusinessRuleCatalog();

        catalog.GetRulesByModule(modulo).Should().Contain(rule => rule.Description.Contains(trecho, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SIAFIC", "Financeiro")]
    [InlineData("TRIBUTARIO", "Tributário")]
    [InlineData("PROTOCOLOS", "Processos")]
    public void Catalogo_Deve_Normalizar_Apelidos_Do_Lote_2(string apelido, string moduloEsperado)
    {
        var catalog = new BusinessRuleCatalog();

        catalog.GetRulesByModule(apelido).Should().OnlyContain(rule => rule.Module == moduloEsperado);
    }
}
