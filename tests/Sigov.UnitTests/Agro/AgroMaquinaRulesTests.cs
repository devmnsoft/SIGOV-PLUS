using FluentAssertions;
using Sigov.Domain.Agro;
using Sigov.Domain.Agro.Enums;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class AgroMaquinaRulesTests
{
    [Fact] public void Maquina_exige_codigo() => FluentActions.Invoking(() => new MaquinaRural(1,1,"","Trator",AgroMaquinaTipo.TRATOR,AgroMaquinaSituacao.ATIVA)).Should().Throw<ArgumentException>();
    [Fact] public void Maquina_exige_nome() => FluentActions.Invoking(() => new MaquinaRural(1,1,"M1","",AgroMaquinaTipo.TRATOR,AgroMaquinaSituacao.ATIVA)).Should().Throw<ArgumentException>();
    [Fact] public void Maquina_inativa_nao_pode_ser_agendada() { var m = new MaquinaRural(1,1,"M1","Trator",AgroMaquinaTipo.TRATOR,AgroMaquinaSituacao.INATIVA); FluentActions.Invoking(m.ValidarPodeAgendar).Should().Throw<InvalidOperationException>(); }
}
