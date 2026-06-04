using FluentAssertions;
using Sigov.Domain.Saas;
using Xunit;

namespace Sigov.UnitTests;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("municipio-demo")]
    [InlineData("camara-2026")]
    public void Create_DeveAceitarSlugValido(string value)
    {
        var result = TenantSlug.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Municipio Demo")]
    [InlineData("-municipio")]
    [InlineData("municipio-")]
    public void Create_DeveRecusarSlugInvalido(string value)
    {
        TenantSlug.Create(value).IsFailure.Should().BeTrue();
    }
}
