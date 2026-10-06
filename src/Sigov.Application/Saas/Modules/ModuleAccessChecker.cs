using Sigov.Domain.Saas;

namespace Sigov.Application.Saas.Modules;

public sealed class ModuleAccessChecker : IModuleAccessChecker
{
    private static readonly ISet<string> ImplantacaoProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PerfilNivelCodigos.AdministradorGeral,
        PerfilNivelCodigos.AdministradorTenant,
        PerfilNivelCodigos.Suporte
    };

    private readonly IModuleCatalogService _catalogService;
    private readonly IModuleAccessRepository _repository;

    public ModuleAccessChecker(IModuleCatalogService catalogService, IModuleAccessRepository repository)
    {
        _catalogService = catalogService;
        _repository = repository;
    }

    public async Task<ModuleAccessResult> CheckModuleAsync(ModuleAccessRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleCode))
        {
            return ModuleAccessResult.Forbidden("Módulo não informado.");
        }

        var isGlobalAdmin = request.ProfileCodes.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);
        if (isGlobalAdmin && request.TenantId is null)
        {
            return ModuleAccessResult.Allow("Administrador geral pode visualizar o catálogo global.");
        }

        if (isGlobalAdmin && !request.HasAuditedTenantContext)
        {
            return ModuleAccessResult.Forbidden("Administrador geral precisa de troca de contexto auditada para atuar em tenant.", SaasForbiddenMotivo.ForaEscopo);
        }

        if (request.TenantId is null)
        {
            return ModuleAccessResult.Forbidden("Usuário comum precisa estar vinculado a um tenant ativo.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tenantStatus = await _repository.GetTenantStatusAsync(request.TenantId.Value, cancellationToken).ConfigureAwait(false);
        if (TenantCommercialStatus.IsBlocked(tenantStatus))
        {
            return ModuleAccessResult.Forbidden($"Cliente em situação comercial bloqueante ({tenantStatus}).", SaasForbiddenMotivo.BloqueadoComercial);
        }

        var subscription = await _repository.GetActiveSubscriptionAsync(request.TenantId.Value, cancellationToken).ConfigureAwait(false);
        if (subscription is not null)
        {
            var assinaturaAtiva = string.Equals(subscription.Status, "ATIVA", StringComparison.OrdinalIgnoreCase);
            if (!assinaturaAtiva)
            {
                return ModuleAccessResult.Forbidden($"Assinatura comercial do cliente com status {subscription.Status}; acesso suspenso.", SaasForbiddenMotivo.BloqueadoComercial);
            }

            if (subscription.ValidUntil.HasValue && subscription.ValidUntil.Value < today)
            {
                return ModuleAccessResult.Forbidden("Vigência da assinatura comercial expirada.", SaasForbiddenMotivo.ContratoExpirado);
            }
        }

        var module = await _catalogService.FindByCodeAsync(request.ModuleCode, cancellationToken).ConfigureAwait(false);
        if (module is null)
        {
            return ModuleAccessResult.Forbidden("Módulo não existe no catálogo vendável do sigov.", SaasForbiddenMotivo.ModuloNaoContratado);
        }

        var contract = await _repository.GetTenantModuleAsync(request.TenantId.Value, module.Codigo, cancellationToken).ConfigureAwait(false);
        if (contract is null || !contract.Active)
        {
            return ModuleAccessResult.Forbidden("Módulo não contratado para o tenant.", SaasForbiddenMotivo.ModuloNaoContratado);
        }

        if (contract.EffectiveFrom.HasValue && contract.EffectiveFrom.Value > today)
        {
            return ModuleAccessResult.Forbidden("Vigência do módulo ainda não iniciada.", SaasForbiddenMotivo.ContratoExpirado);
        }

        if (contract.EffectiveUntil.HasValue && contract.EffectiveUntil.Value < today)
        {
            return ModuleAccessResult.Forbidden("Módulo expirado para o tenant.", SaasForbiddenMotivo.ContratoExpirado);
        }

        if (string.Equals(contract.Status, "SUSPENSO", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleAccessResult.Forbidden("Módulo suspenso para o tenant.", SaasForbiddenMotivo.BloqueadoComercial);
        }

        if (string.Equals(contract.Status, "CANCELADO", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleAccessResult.Forbidden("Módulo cancelado para o tenant.", SaasForbiddenMotivo.BloqueadoComercial);
        }

        if (string.Equals(contract.Status, "INADIMPLENTE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contract.Status, "EXPIRADO", StringComparison.OrdinalIgnoreCase))
        {
            return ModuleAccessResult.Forbidden("Módulo indisponível por situação comercial.", SaasForbiddenMotivo.BloqueadoComercial);
        }

        if (string.Equals(contract.Status, "EM_IMPLANTACAO", StringComparison.OrdinalIgnoreCase) && !request.ProfileCodes.Any(ImplantacaoProfiles.Contains))
        {
            return ModuleAccessResult.Forbidden("Módulo em implantação exige perfil autorizado.", SaasForbiddenMotivo.ForaEscopo);
        }

        if (!IsEnabledStatus(contract.Status))
        {
            return ModuleAccessResult.Forbidden("Módulo ainda não está habilitado para operação.", SaasForbiddenMotivo.ModuloNaoContratado);
        }

        foreach (var dependency in module.Dependencias)
        {
            var dependencyContract = await _repository.GetTenantModuleAsync(request.TenantId.Value, dependency, cancellationToken).ConfigureAwait(false);
            if (dependencyContract is null || !IsEffectiveContract(dependencyContract, today))
            {
                return ModuleAccessResult.Forbidden($"Dependência de módulo não atendida: {dependency}.", SaasForbiddenMotivo.ModuloNaoContratado);
            }
        }

        return ModuleAccessResult.Allow();
    }

    public async Task<ModuleAccessResult> CheckFeatureAsync(ModuleAccessRequest request, string featureCode, CancellationToken cancellationToken)
    {
        var moduleResult = await CheckModuleAsync(request, cancellationToken).ConfigureAwait(false);
        if (!moduleResult.Allowed)
        {
            return moduleResult;
        }

        if (request.TenantId is null)
        {
            return ModuleAccessResult.Allow("Catálogo global não avalia feature de tenant.");
        }

        var enabled = await _repository.IsFeatureEnabledAsync(request.TenantId.Value, request.ModuleCode, featureCode, cancellationToken).ConfigureAwait(false);
        return enabled ? ModuleAccessResult.Allow("Feature habilitada.") : ModuleAccessResult.Forbidden("Feature desabilitada para o tenant.", SaasForbiddenMotivo.ForaEscopo);
    }

    private static bool IsEnabledStatus(string status) => string.Equals(status, "HABILITADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "CONTRATADO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "ATIVO", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "TRIAL", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "BETA", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "EM_IMPLANTACAO", StringComparison.OrdinalIgnoreCase);

    private static bool IsEffectiveContract(TenantModuleContract contract, DateOnly today) =>
        contract.Active && IsEnabledStatus(contract.Status) &&
        !string.Equals(contract.Status, "INADIMPLENTE", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(contract.Status, "EXPIRADO", StringComparison.OrdinalIgnoreCase) &&
        (!contract.EffectiveFrom.HasValue || contract.EffectiveFrom.Value <= today) &&
        (!contract.EffectiveUntil.HasValue || contract.EffectiveUntil.Value >= today);
}
