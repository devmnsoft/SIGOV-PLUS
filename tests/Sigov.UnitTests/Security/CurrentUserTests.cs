using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Sigov.Infrastructure.Security;

namespace Sigov.UnitTests.Security;

public sealed class CurrentUserTests
{
    [Fact]
    public void Authenticated_user_resolves_compatibility_claims_in_defined_order()
    {
        var principal = Principal(
            new Claim("sub", "22"),
            new Claim(ClaimTypes.NameIdentifier, "11"),
            new Claim("usuario_id", "33"),
            new Claim("login", "fallback"),
            new Claim(ClaimTypes.Name, "Maria"));

        var currentUser = Create(principal);

        currentUser.IsAuthenticated.Should().BeTrue();
        currentUser.UsuarioId.Should().Be(11);
        currentUser.Nome.Should().Be("Maria");
    }

    [Fact]
    public void Anonymous_user_has_no_identity_data()
    {
        var currentUser = Create(new ClaimsPrincipal(new ClaimsIdentity()));

        currentUser.IsAuthenticated.Should().BeFalse();
        currentUser.UsuarioId.Should().BeNull();
        currentUser.Nome.Should().BeNull();
    }

    [Fact]
    public void Invalid_or_non_positive_identifier_is_ignored_and_valid_alias_is_used()
    {
        var currentUser = Create(Principal(
            new Claim(ClaimTypes.NameIdentifier, "not-a-number"),
            new Claim("sub", "0"),
            new Claim("usuario_id", "42"),
            new Claim("name", "João")));

        currentUser.UsuarioId.Should().Be(42);
        currentUser.Nome.Should().Be("João");
    }

    private static CurrentUser Create(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Test"));
}
