namespace Sigov.Application.Saas.Comercial;

public interface ISaasTenantProvisioningService
{
    Task<long> ConverterSolicitacaoAsync(long solicitacaoId, ConverterSolicitacaoEmTenantRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ISaasTenantProvisioningRepository
{
    Task<long> ConverterSolicitacaoAsync(long solicitacaoId, ConverterSolicitacaoEmTenantRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
}
