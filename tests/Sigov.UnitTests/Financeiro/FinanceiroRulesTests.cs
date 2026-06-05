using FluentAssertions;
using Sigov.Domain.Financeiro;
using Xunit;

namespace Sigov.UnitTests.Financeiro;

public sealed class OrcamentoDespesaRulesTests
{
    [Fact] public void Orcamento_Nao_Aceita_Dotacao_Negativa() => Assert.Throws<InvalidOperationException>(() => new OrcamentoDespesa(1, 1, 1, -1m));
    [Fact] public void Saldo_Disponivel_Considera_Suplementacoes_Reducoes_Reservado_E_Empenhado() { var o = new OrcamentoDespesa(1, 1, 1, 100m, 50m, 10m, 5m, 20m); o.SaldoDisponivel.Should().Be(115m); }
}
public sealed class EmpenhoRulesTests
{
    [Fact] public void Empenho_Exige_Valor_Maior_Que_Zero() => Assert.Throws<InvalidOperationException>(() => new Empenho(1, 1, 1, 10, 0m));
    [Fact] public void Empenho_Exige_Fornecedor() => Assert.Throws<InvalidOperationException>(() => new Empenho(1, 1, 1, 0, 10m));
    [Fact] public void Empenho_Nao_Ultrapassa_Saldo() { var o = new OrcamentoDespesa(1, 1, 1, 100m); var e = new Empenho(1, 1, 1, 9, 101m); Assert.Throws<InvalidOperationException>(() => e.ValidarContraOrcamento(o, false)); }
    [Fact] public void Empenho_Anulado_Nao_Liquida() { var e = new Empenho(1, 1, 1, 9, 100m); e.Anular(100m); Assert.Throws<InvalidOperationException>(() => e.RegistrarLiquidacao(1m, false)); }
}
public sealed class LiquidacaoRulesTests
{
    [Fact] public void Liquidacao_Nao_Ultrapassa_Saldo_Do_Empenho() { var e = new Empenho(1, 1, 1, 9, 100m); e.RegistrarLiquidacao(80m, false); Assert.Throws<InvalidOperationException>(() => e.RegistrarLiquidacao(21m, false)); }
}
public sealed class PagamentoRulesTests
{
    [Fact] public void Pagamento_Nao_Ultrapassa_Saldo_Liquidado() { var e = new Empenho(1, 1, 1, 9, 100m); e.RegistrarLiquidacao(50m, false); Assert.Throws<InvalidOperationException>(() => e.RegistrarPagamento(51m, false)); }
}
public sealed class ReceitaRulesTests
{
    [Fact] public void Receita_Lancada_Exige_Valor_Maior_Que_Zero() => Assert.Throws<InvalidOperationException>(() => new ReceitaLancamento(1, 1, 1, 0m));
    [Fact] public void Arrecadacao_Nao_Ultrapassa_Saldo_Lancado() { var r = new ReceitaLancamento(1, 1, 1, 100m); r.RegistrarArrecadacao(90m, false); Assert.Throws<InvalidOperationException>(() => r.RegistrarArrecadacao(11m, false)); }
}
public sealed class FinanceiroSaldoTests
{
    [Fact] public void Exercicio_Encerrado_Bloqueia_Operacoes() { var o = new OrcamentoDespesa(1, 1, 1, 100m); Assert.Throws<InvalidOperationException>(() => o.ValidarEmpenho(10m, true)); }
}
