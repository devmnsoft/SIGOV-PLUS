namespace Sigov.Application.Saas.Modules;

public interface IModuleEntitlementEvaluator
{
    Task<ModuleEntitlementDecision> EvaluateAsync(ModuleEntitlementRequest request, CancellationToken cancellationToken = default);
}

public sealed record ModuleEntitlementRequest(
    long UserId,
    string ModuleCode,
    IReadOnlyCollection<string> ProfileCodes,
    long? TenantId,
    long? EntidadeId = null,
    long? ExercicioId = null,
    string? Permission = null,
    bool HasAuditedTenantContext = false,
    string? CorrelationId = null);

public sealed record ModuleEntitlementDecision(bool Allowed, string Reason)
{
    public static ModuleEntitlementDecision Allow(string reason = "Acesso permitido.") => new(true, reason);
    public static ModuleEntitlementDecision Deny(string reason) => new(false, reason);
}
