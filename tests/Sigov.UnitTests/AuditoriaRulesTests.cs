using FluentAssertions;
using Sigov.Application.Auditoria;
using Xunit;

namespace Sigov.UnitTests;

public sealed class AuditoriaRulesTests
{
    [Fact]
    public void Periodo_De_Auditoria_Deve_Ser_Valido()
    {
        new AuditoriaFiltroValidator().IsPeriodoValido(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2)).Should().BeTrue();
    }
}
