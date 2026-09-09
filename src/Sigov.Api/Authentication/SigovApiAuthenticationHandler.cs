using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Sigov.Api.Authentication;

public sealed class SigovApiAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SigovApi";

    public SigovApiAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.User.Identity?.IsAuthenticated == true
            && string.Equals(Context.User.Identity.AuthenticationType, SchemeName, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Context.User, SchemeName)));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
