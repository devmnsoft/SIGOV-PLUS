using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Sigov.Application.Configuration;

public sealed class SigovOptionsValidator : IValidateOptions<SigovOptions>
{
    private readonly IHostEnvironment _environment;

    public SigovOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, SigovOptions options)
    {
        if (!string.Equals(options.Database.Schema, "sigov", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail("O schema PostgreSQL permitido é exclusivamente sigov.");
        }

        if (!_environment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        if (options.Seed.Demo)
        {
            return ValidateOptionsResult.Fail("Seed demo é proibido em Production.");
        }

        if (string.IsNullOrWhiteSpace(options.Jwt.Secret) || options.Jwt.Secret.Length < 32)
        {
            return ValidateOptionsResult.Fail("Sigov:Jwt:Secret deve ser fornecido por variável de ambiente/secret manager em Production e ter pelo menos 32 caracteres.");
        }

        if (options.Security.CorsAllowedOrigins.Length == 0 || options.Security.CorsAllowedOrigins.Any(origin => origin == "*"))
        {
            return ValidateOptionsResult.Fail("CORS em Production deve declarar origens explícitas e não pode usar wildcard.");
        }

        if (options.Security.SwaggerEnabledInProduction && string.IsNullOrWhiteSpace(options.Security.BootstrapToken))
        {
            return ValidateOptionsResult.Fail("Swagger em Production só pode ser habilitado com proteção explícita.");
        }

        return ValidateOptionsResult.Success;
    }
}
