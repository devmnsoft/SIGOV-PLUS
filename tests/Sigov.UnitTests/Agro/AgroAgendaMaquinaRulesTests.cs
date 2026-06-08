using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroAgendaMaquinaRulesTests
{
    [Fact] public void Agenda_fim_menor_que_inicio_falha() => FluentActions.Invoking(() => new AgendaMaquinaRural(1,1,1,1,DateTimeOffset.Parse("2026-01-02T10:00:00Z"),DateTimeOffset.Parse("2026-01-02T09:00:00Z"),AgroAgendaMaquinaStatus.AGENDADA)).Should().Throw<ArgumentException>();
    [Fact] public void Agenda_sobreposta_para_mesma_maquina_falha() { var a = new AgendaMaquinaRural(1,1,1,1,DateTimeOffset.Parse("2026-01-02T08:00:00Z"),DateTimeOffset.Parse("2026-01-02T10:00:00Z"),AgroAgendaMaquinaStatus.AGENDADA); var b = new AgendaMaquinaRural(1,1,1,1,DateTimeOffset.Parse("2026-01-02T09:00:00Z"),DateTimeOffset.Parse("2026-01-02T11:00:00Z"),AgroAgendaMaquinaStatus.AGENDADA); a.Sobrepoe(b).Should().BeTrue(); }
}
