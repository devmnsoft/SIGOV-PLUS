using FluentAssertions;
using Sigov.Application.Core.Pessoas;
using Xunit;

namespace Sigov.UnitTests.Core;

public sealed class CorePessoaValidationTests
{
    [Fact]
    public void PessoaValidator_Deve_Normalizar_Documento()
    {
        var validator = new PessoaValidator();
        validator.NormalizeDocumento("000.000.001-91").Should().Be("00000000191");
    }
}
