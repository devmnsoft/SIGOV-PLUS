using FluentAssertions;
using Sigov.Web.Services.Common;
using Xunit;

namespace Sigov.UnitTests.Consolidacao;

public sealed class OperationResultTests
{
    [Fact]
    public void Ok_Deve_retorna_sucesso_com_mensagem_e_dados()
    {
        var result = OperationResult.Ok("Fluxo consolidado", new { Id = 10 });
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Fluxo consolidado");
        result.Code.Should().BeNull();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public void Fail_Deve_retorna_falha_com_codigo_funcional()
    {
        var result = OperationResult.Fail("Schema indisponível", "SCHEMA_MISSING");
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Schema indisponível");
        result.Code.Should().Be("SCHEMA_MISSING");
        result.Data.Should().BeNull();
    }
}
