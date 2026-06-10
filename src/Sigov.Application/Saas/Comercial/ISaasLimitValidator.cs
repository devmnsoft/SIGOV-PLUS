namespace Sigov.Application.Saas.Comercial;

public interface ISaasLimitValidator
{
    Task<SaasLimitValidationResult> ValidateUserLimitAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<SaasLimitValidationResult> ValidateModuleLimitAsync(long tenantId, string moduloCodigo, CancellationToken cancellationToken = default);
    Task<SaasLimitValidationResult> ValidateWhiteLabelAllowedAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<SaasUsageSummary> GetUsageSummaryAsync(long tenantId, CancellationToken cancellationToken = default);
}

public sealed record SaasLimitValidationResult(bool Allowed, string? Alert, SaasUsageSummary? Usage);

public sealed record SaasUsageSummary(
    long TenantId,
    string? Plano,
    int UsuariosAtivos,
    int? LimiteUsuarios,
    int ModulosAtivos,
    int? LimiteModulos,
    bool WhiteLabelPermitido,
    bool DominioCustomizadoPermitido,
    decimal PercentualUsoUsuarios);
