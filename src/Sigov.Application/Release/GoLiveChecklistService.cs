namespace Sigov.Application.Release;

public sealed class GoLiveChecklistService : IGoLiveChecklistService
{
    public GoLiveCheckResult Evaluate(GoLiveChecklistContext context)
    {
        var checks = new List<GoLiveCheck>
        {
            Check("environment", IsProduction(context.Environment), "ASPNETCORE_ENVIRONMENT deve ser Production."),
            Check("connection-string", !string.IsNullOrWhiteSpace(context.DefaultConnection), "ConnectionStrings__DefaultConnection deve estar definido por secret/variável."),
            Check("jwt-secret", !string.IsNullOrWhiteSpace(context.JwtSecret) && context.JwtSecret.Length >= 32, "Sigov__Jwt__Secret deve ter pelo menos 32 caracteres."),
            Check("cors", context.CorsAllowedOrigins.Count > 0 && !context.CorsAllowedOrigins.Contains("*", StringComparer.Ordinal), "CORS deve usar origens explícitas, sem wildcard."),
            Check("swagger", !context.SwaggerEnabledInProduction, "Swagger deve estar desabilitado ou protegido em Production."),
            Check("seed-demo", !context.SeedDemoEnabled, "Seed demo deve estar desabilitado em Production."),
            Check("admin-default", !context.AdminDefaultEnabled, "Admin default deve estar desabilitado em Production."),
            Check("dev-adapters", !context.DevelopmentAdaptersEnabled, "Adapters fake/dev devem estar desabilitados em Production."),
            Check("https", context.HttpsConfigured, "HTTPS/reverse proxy seguro deve estar configurado."),
            Check("restore", context.RestoreProtected, "Restore deve exigir confirmação explícita.")
        };

        checks.Add(context.BackupConfigured
            ? new GoLiveCheck("backup", GoLiveCheckStatus.Pass, "Backup configurado.")
            : new GoLiveCheck("backup", GoLiveCheckStatus.Warn, "Backup deve ser validado antes do go-live."));

        return new GoLiveCheckResult(checks);
    }

    private static bool IsProduction(string value) => string.Equals(value, "Production", StringComparison.Ordinal);

    private static GoLiveCheck Check(string name, bool passed, string message) =>
        new(name, passed ? GoLiveCheckStatus.Pass : GoLiveCheckStatus.Fail, message);
}
