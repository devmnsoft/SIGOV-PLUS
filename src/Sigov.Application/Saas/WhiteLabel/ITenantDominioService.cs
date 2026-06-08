using Sigov.Domain.Common;

namespace Sigov.Application.Saas.WhiteLabel;

public interface ITenantDominioService
{
    Task<IReadOnlyCollection<TenantDominioResponse>> ListAsync(long tenantId, CancellationToken cancellationToken);
    Task<Result<TenantDominioResponse>> CreateAsync(long tenantId, TenantDominioCreateRequest request, long usuarioId, CancellationToken cancellationToken);
    Task<Result<TenantDominioResponse>> VerificarAsync(long tenantId, long id, VerificarDominioRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ITenantDominioRepository
{
    Task<IReadOnlyCollection<TenantDominioResponse>> ListAsync(long tenantId, CancellationToken cancellationToken);
    Task<bool> PlanoPermiteDominioAsync(long tenantId, CancellationToken cancellationToken);
    Task<TenantDominioResponse> CreateAsync(long tenantId, string dominio, string tokenHash, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task<TenantDominioResponse> VerifyAsync(long tenantId, long id, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
