using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.WhiteLabel;

public sealed class TenantDominio : Entity
{
    public TenantDominio(long id, long tenantId, string dominio, TenantDominioStatus status)
    {
        Id = id;
        TenantId = tenantId;
        Dominio = dominio?.Trim().ToLowerInvariant() ?? string.Empty;
        Status = status;
    }

    public long TenantId { get; }
    public string Dominio { get; }
    public TenantDominioStatus Status { get; }

    public Result Validate(bool planoOuAddonPermiteDominio)
    {
        if (TenantId <= 0) return Result.Failure("Domínio exige tenant.");
        if (string.IsNullOrWhiteSpace(Dominio) || !Dominio.Contains('.', StringComparison.Ordinal)) return Result.Failure("Domínio customizado válido é obrigatório.");
        return planoOuAddonPermiteDominio ? Result.Success() : Result.Failure("Plano ou addon não permite domínio customizado.");
    }
}
