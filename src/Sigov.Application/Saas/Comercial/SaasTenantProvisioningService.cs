using Microsoft.Extensions.Logging;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasTenantProvisioningService : ISaasTenantProvisioningService
{
    private readonly ISaasTenantProvisioningRepository _repository;
    private readonly ILogger<SaasTenantProvisioningService> _logger;

    public SaasTenantProvisioningService(ISaasTenantProvisioningRepository repository, ILogger<SaasTenantProvisioningService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<long> ConverterSolicitacaoAsync(long solicitacaoId, ConverterSolicitacaoEmTenantRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        var tenantId = await _repository.ConverterSolicitacaoAsync(solicitacaoId, request, usuarioId, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Solicitação {SolicitacaoId} convertida no tenant {TenantId} com onboarding e perfis base.", solicitacaoId, tenantId);
        return tenantId;
    }
}
