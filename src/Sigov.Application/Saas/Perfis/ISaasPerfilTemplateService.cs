using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Perfis;

public interface ISaasPerfilTemplateService
{
    Task<IReadOnlyCollection<SaasPerfilTemplateResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<SaasPerfilTemplateResponse>> CreateAsync(SaasPerfilTemplateResponse request, long usuarioId, CancellationToken cancellationToken);
    Task<Result> CriarPerfisTenantPorTemplateAsync(CriarPerfisTenantPorTemplateRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ISaasPerfilTemplateRepository
{
    Task<IReadOnlyCollection<SaasPerfilTemplateResponse>> ListAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<SaasPerfilTemplateResponse> CreateAsync(SaasPerfilTemplateResponse request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task CriarPerfisTenantPorTemplateAsync(CriarPerfisTenantPorTemplateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
