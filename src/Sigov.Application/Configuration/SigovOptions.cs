namespace Sigov.Application.Configuration;

public sealed class SigovOptions
{
    public DatabaseOptions Database { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public JwtOptions Jwt { get; set; } = new();
    public TenantOptions Tenant { get; set; } = new();
    public SeedOptions Seed { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public EmailOptions Email { get; set; } = new();
    public BackupOptions Backup { get; set; } = new();
    public ObservabilityOptions Observability { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
}

public sealed class DatabaseOptions
{
    public string Schema { get; set; } = "sigov";
    public bool RunMigrationsOnStartup { get; set; }
    public string MigrationsPath { get; set; } = "database/postgres/migrations";
    public string MigrationMode { get; set; } = "Disabled";
}

public sealed class SecurityOptions
{
    public bool RequireProductionSecrets { get; set; } = true;
    public string? BootstrapToken { get; set; }
    public bool SwaggerEnabledInProduction { get; set; }
    public string[] CorsAllowedOrigins { get; set; } = Array.Empty<string>();
}

public sealed class JwtOptions
{
    public string? Secret { get; set; }
    public string Issuer { get; set; } = "sigov";
    public string Audience { get; set; } = "sigov";
}

public sealed class TenantOptions
{
    public string HeaderName { get; set; } = "X-Sigov-Tenant";
    public bool AllowHeaderResolution { get; set; }
    public bool AllowQueryStringResolution { get; set; }
    public string BaseDomain { get; set; } = "sigov.local";
}

public sealed class SeedOptions
{
    public bool Demo { get; set; }
}

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string LocalPath { get; set; } = "./storage";
    public long MaxUploadBytes { get; set; } = 10485760;
}

public sealed class EmailOptions
{
    public string Provider { get; set; } = "Development";
}

public sealed class BackupOptions
{
    public string Directory { get; set; } = "./backups";
    public int RetentionDays { get; set; } = 30;
}

public sealed class ObservabilityOptions
{
    public bool StructuredLogs { get; set; } = true;
    public bool MaskSensitiveData { get; set; } = true;
}

public sealed class RateLimitOptions
{
    public int RequestsPerMinutePerTenant { get; set; } = 600;
    public int LoginAttemptsPerMinute { get; set; } = 10;
}
