using Sigov.Application.Authorization;
using Sigov.Application.Saas.Modules;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Infrastructure.Saas;

public sealed class ModuleEntitlementEvaluator(
    IModuleAccessChecker accessChecker,
    IAuthorizationEvaluator authorization) : IModuleEntitlementEvaluator
{
    public async Task<ModuleEntitlementDecision> EvaluateAsync(ModuleEntitlementRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleCode))
            return ModuleEntitlementDecision.Deny("Módulo não informado.");

        var module = await accessChecker.CheckModuleAsync(
            new ModuleAccessRequest(request.TenantId, request.ModuleCode, request.ProfileCodes, request.HasAuditedTenantContext),
            cancellationToken).ConfigureAwait(false);
        if (!module.Allowed)
            return ModuleEntitlementDecision.Deny(module.Reason);

        if (string.IsNullOrWhiteSpace(request.Permission))
            return ModuleEntitlementDecision.Allow(module.Reason);

        if (request.UserId <= 0)
            return ModuleEntitlementDecision.Deny("Identidade obrigatória para avaliar permissão do módulo.");

        var separator = request.Permission.LastIndexOf('.');
        var resource = separator > 0 ? request.Permission[..separator] : request.Permission;
        var action = separator > 0 ? request.Permission[(separator + 1)..] : "acessar";
        var moduleSeparator = resource.IndexOf('.');
        if (moduleSeparator > 0)
            resource = resource[(moduleSeparator + 1)..];

        var decision = await authorization.EvaluateAsync(new AuthorizationRequest(
            request.UserId,
            request.ModuleCode,
            resource,
            action,
            request.TenantId,
            request.EntidadeId,
            request.ExercicioId,
            CorrelationId: request.CorrelationId,
            Origem: "ENTITLEMENT"), cancellationToken).ConfigureAwait(false);
        return decision.Permitido
            ? ModuleEntitlementDecision.Allow("Contrato e permissão vigentes.")
            : ModuleEntitlementDecision.Deny("Permissão insuficiente para o módulo.");
    }
}
