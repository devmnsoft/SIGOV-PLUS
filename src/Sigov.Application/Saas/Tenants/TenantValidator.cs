namespace Sigov.Application.Saas.Tenants;

public sealed class TenantValidator
{
    public bool IsOperacaoPermitida(string status) => !string.Equals(status, "SUSPENSO", StringComparison.OrdinalIgnoreCase) && !string.Equals(status, "CANCELADO", StringComparison.OrdinalIgnoreCase);
}
