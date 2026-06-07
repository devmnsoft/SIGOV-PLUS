namespace Sigov.Application.Saas.Tenants;

public sealed class TenantProvisioningValidator
{
    public bool HasValidDomain(string? domain) => !string.IsNullOrWhiteSpace(domain) && domain.Contains('.', StringComparison.Ordinal);
}
