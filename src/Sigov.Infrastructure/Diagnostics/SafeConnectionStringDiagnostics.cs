using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Sigov.Infrastructure.Diagnostics;

public sealed record SafeDatabaseTarget(
    string Host,
    int Port,
    string Database,
    string Username,
    string? SearchPath,
    string? ApplicationName,
    string Environment,
    string ContentRoot,
    string AspNetCoreEnvironment,
    string ConfigurationSource)
{
    public string Endpoint => $"{Host}:{Port}/{Database}";
}

public static class SafeConnectionStringDiagnostics
{
    public static SafeDatabaseTarget ValidateDevelopmentTarget(
        IConfiguration configuration,
        IHostEnvironment environment,
        string applicationName)
    {
        var target = Read(configuration, environment);
        if (!environment.IsDevelopment()) return target;

        var expectedApplicationName = $"sigov.{applicationName.ToLowerInvariant()}";
        if (string.IsNullOrWhiteSpace(target.Host))
            throw new InvalidOperationException("Configuração inválida: Host não está definido.");
        if (string.IsNullOrWhiteSpace(target.Database))
            throw new InvalidOperationException("Configuração inválida: Database não está definido.");
        if (string.IsNullOrWhiteSpace(target.Username))
            throw new InvalidOperationException("Configuração inválida: Username não está definido.");
        if (!ContainsSchema(target.SearchPath, "sigov"))
            throw new InvalidOperationException("Configuração inválida: o schema sigov não está definido no Search Path. Use Search Path=sigov.");
        if (!string.Equals(target.ApplicationName, expectedApplicationName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Configuração inválida: Application Name do Sigov.{applicationName} deve ser {expectedApplicationName}.");

        return target;
    }

    public static SafeDatabaseTarget Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
        var connection = new NpgsqlConnectionStringBuilder(raw);
        var source = ResolveSource(configuration);
        var host = connection.Host ?? string.Empty;
        var database = connection.Database ?? string.Empty;
        var username = connection.Username ?? string.Empty;
        var aspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? environment.EnvironmentName;

        return new SafeDatabaseTarget(
            host,
            connection.Port,
            database,
            username,
            connection.SearchPath,
            connection.ApplicationName,
            environment.EnvironmentName,
            environment.ContentRootPath,
            aspNetEnvironment,
            source);
    }

    private static bool ContainsSchema(string? searchPath, string schema) =>
        !string.IsNullOrWhiteSpace(searchPath) && searchPath.Split(',')
            .Select(item => item.Trim().Trim('"'))
            .Any(item => string.Equals(item, schema, StringComparison.OrdinalIgnoreCase));

    public static void LogTarget(ILogger logger, SafeDatabaseTarget target, string application)
    {
        logger.LogInformation(
            "SIGOV DatabaseTarget Application={Application}; Host={Host}; Port={Port}; Database={Database}; Username={Username}; SearchPath={SearchPath}; ApplicationName={ApplicationName}; Environment={Environment}; ContentRoot={ContentRoot}; ASPNETCORE_ENVIRONMENT={AspNetCoreEnvironment}; ConfigurationSource={ConfigurationSource}; Password=<redacted>",
            application, target.Host, target.Port, target.Database, target.Username, target.SearchPath,
            target.ApplicationName, target.Environment, target.ContentRoot, target.AspNetCoreEnvironment,
            target.ConfigurationSource);
    }

    private static string ResolveSource(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            return "Environment:ConnectionStrings__DefaultConnection";
        if (configuration is not IConfigurationRoot root) return "Configuration (provider não identificado)";

        var providers = root.Providers.Reverse()
            .Where(provider => provider.TryGet("ConnectionStrings:DefaultConnection", out _))
            .Select(provider => provider.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name));
        return string.Join(" -> ", providers!);
    }
}
