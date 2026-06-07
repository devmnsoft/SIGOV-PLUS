using FluentAssertions;
using Sigov.Application.Seguranca.Usuarios;
using Xunit;

namespace Sigov.UnitTests;

public sealed class SecurityRulesTests
{
    [Fact]
    public void UsuarioMapper_Deve_Mascarar_Email()
    {
        new UsuarioMapper().MaskEmail("admin@sigov.local").Should().Be("a***@sigov.local");
    }
}
