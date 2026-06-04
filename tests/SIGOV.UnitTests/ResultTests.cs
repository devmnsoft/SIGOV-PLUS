using FluentAssertions;
using SIGOV.Domain.Common;
using Xunit;

namespace SIGOV.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void Failure_Deve_Registrar_Erro_De_Negocio()
    {
        var result = Result.Failure("regra inválida");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("regra inválida");
    }
}
