using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public interface ISaasSolicitacaoClienteService
{
    Task<Result<SaasSolicitacaoClienteResponse>> CriarAsync(SaasSolicitacaoClienteCreateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasSolicitacaoClienteResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<SaasSolicitacaoClienteResponse?> GetAdminAsync(long id, CancellationToken cancellationToken);
    Task<Result> AprovarAsync(long id, AprovarSolicitacaoClienteRequest request, long usuarioId, CancellationToken cancellationToken);
    Task<Result<long>> ConverterEmTenantAsync(long id, ConverterSolicitacaoEmTenantRequest request, long usuarioId, CancellationToken cancellationToken);
    Task<Result> RecusarAsync(long id, RecusarSolicitacaoClienteRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ISaasSolicitacaoClienteRepository
{
    Task<SaasSolicitacaoClienteResponse> CreateAsync(SaasSolicitacaoClienteCreateRequest request, string protocolo, Guid correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasSolicitacaoClienteResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<SaasSolicitacaoClienteResponse?> GetAdminAsync(long id, CancellationToken cancellationToken);
    Task UpdateStatusAsync(long id, string status, string? observacao, long? tenantId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
