using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class ProcessoMovimentacaoRulesTests
{
    [Fact] public void Movimentacao_Exige_Despacho() { Action act = () => new ProcessoMovimentacao(" "); act.Should().Throw<ArgumentException>().WithMessage("*despacho*"); }
}
