using FluentAssertions;
using Sigov.Application.Release;
using Xunit;

namespace Sigov.UnitTests.Release;

public sealed class GoLiveChecklistServiceTests
{
    [Fact]
    public void Evaluate_Deve_Falhar_Sem_Jwt_Secret_Em_Production()
    {
        var result = new GoLiveChecklistService().Evaluate(CreateContext(jwtSecret: null));

        result.Checks.Should().Contain(check => check.Name == "jwt-secret" && check.Status == GoLiveCheckStatus.Fail);
    }

    [Fact]
    public void Evaluate_Deve_Falhar_Com_Cors_Wildcard()
    {
        var result = new GoLiveChecklistService().Evaluate(CreateContext(cors: new[] { "*" }));

        result.Checks.Should().Contain(check => check.Name == "cors" && check.Status == GoLiveCheckStatus.Fail);
    }

    [Fact]
    public void Evaluate_Deve_Falhar_Com_Seed_Demo()
    {
        var result = new GoLiveChecklistService().Evaluate(CreateContext(seedDemo: true));

        result.Checks.Should().Contain(check => check.Name == "seed-demo" && check.Status == GoLiveCheckStatus.Fail);
    }

    [Fact]
    public void Evaluate_Deve_Alertar_Sem_Backup()
    {
        var result = new GoLiveChecklistService().Evaluate(CreateContext(backupConfigured: false));

        result.Checks.Should().Contain(check => check.Name == "backup" && check.Status == GoLiveCheckStatus.Warn);
    }

    private static GoLiveChecklistContext CreateContext(
        string? jwtSecret = "12345678901234567890123456789012",
        IReadOnlyCollection<string>? cors = null,
        bool seedDemo = false,
        bool backupConfigured = true) => new(
            Environment: "Production",
            DefaultConnection: "Host=db;Database=sigov;Username=sigov;Password=from-secret",
            JwtSecret: jwtSecret,
            CorsAllowedOrigins: cors ?? new[] { "https://app.example.gov.br" },
            SwaggerEnabledInProduction: false,
            SeedDemoEnabled: seedDemo,
            AdminDefaultEnabled: false,
            DevelopmentAdaptersEnabled: false,
            HttpsConfigured: true,
            BackupConfigured: backupConfigured,
            RestoreProtected: true);
}
