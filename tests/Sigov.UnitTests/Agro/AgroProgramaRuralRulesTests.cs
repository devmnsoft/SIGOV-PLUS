using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroProgramaRuralRulesTests
{
    [Fact] public void Programa_exige_codigo() => FluentActions.Invoking(() => new ProgramaRural(1,1,"","Programa",AgroProgramaTipo.OUTROS)).Should().Throw<ArgumentException>();
    [Fact] public void Programa_exige_nome() => FluentActions.Invoking(() => new ProgramaRural(1,1,"PRG","",AgroProgramaTipo.OUTROS)).Should().Throw<ArgumentException>();
    [Fact] public void Vigencia_final_menor_que_inicial_falha() => FluentActions.Invoking(() => new ProgramaRural(1,1,"PRG","Programa",AgroProgramaTipo.OUTROS,new DateOnly(2026,2,1),new DateOnly(2026,1,1))).Should().Throw<ArgumentException>();
}
