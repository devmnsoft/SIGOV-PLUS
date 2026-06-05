using FluentAssertions;
using Sigov.Domain.Processos;
using Xunit;

namespace Sigov.UnitTests.Processos;

public sealed class ProcessoParecerRulesTests
{
    [Fact] public void Parecer_Exige_Texto() { Action act = () => new ProcessoParecer("Título", " "); act.Should().Throw<ArgumentException>().WithMessage("*texto*"); }
}
