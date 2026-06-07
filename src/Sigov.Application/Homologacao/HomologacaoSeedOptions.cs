namespace Sigov.Application.Homologacao;

public sealed record HomologacaoSeedOptions(string TenantSlug, string AdminEmail, string? AdminPassword, bool EnableDemoData)
{
    public static HomologacaoSeedOptions FromEnvironment() => new(
        TenantSlug: GetValue("SIGOV_HML_TENANT_SLUG") ?? "homologacao",
        AdminEmail: GetValue("SIGOV_HML_ADMIN_EMAIL") ?? "admin.hml@sigov.local",
        AdminPassword: GetValue("SIGOV_HML_ADMIN_PASSWORD"),
        EnableDemoData: string.Equals(GetValue("SIGOV_HML_ENABLE_DEMO_DATA"), "true", StringComparison.OrdinalIgnoreCase));

    private static string? GetValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
