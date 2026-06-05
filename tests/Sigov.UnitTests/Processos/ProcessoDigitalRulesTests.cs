using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class ProcessoDigitalRulesTests
{
    [Fact] public void Processo_Encerrado_Nao_Deve_Movimentar() { var p = new ProcessoDigital(1, 1, "PROC-2026-000001", 2026, "Assunto", ProcessoPrioridade.NORMAL, false); p.Encerrar(); Action act = () => p.Movimentar("Despacho"); act.Should().Throw<InvalidOperationException>().WithMessage("*encerrado*"); }
    [Fact] public void Processo_Cancelado_Nao_Deve_Movimentar() { var p = new ProcessoDigital(1, 1, "PROC-2026-000001", 2026, "Assunto", ProcessoPrioridade.NORMAL, false); p.Cancelar(); Action act = () => p.Movimentar("Despacho"); act.Should().Throw<InvalidOperationException>().WithMessage("*cancelado*"); }
    [Fact] public void Processo_Sigiloso_Exige_Permissao_Especifica() { var p = new ProcessoDigital(1, 1, "PROC-2026-000001", 2026, "Assunto", ProcessoPrioridade.NORMAL, true); p.PodeVisualizar(false).Should().BeFalse(); p.PodeVisualizar(true).Should().BeTrue(); }
}
