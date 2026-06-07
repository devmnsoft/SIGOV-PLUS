namespace Sigov.Application.Release;

public interface IGoLiveChecklistService
{
    GoLiveCheckResult Evaluate(GoLiveChecklistContext context);
}

public sealed record GoLiveChecklistContext(
    string Environment,
    string? DefaultConnection,
    string? JwtSecret,
    IReadOnlyCollection<string> CorsAllowedOrigins,
    bool SwaggerEnabledInProduction,
    bool SeedDemoEnabled,
    bool AdminDefaultEnabled,
    bool DevelopmentAdaptersEnabled,
    bool HttpsConfigured,
    bool BackupConfigured,
    bool RestoreProtected);
