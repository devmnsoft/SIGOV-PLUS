namespace Sigov.Application.Saas.Context;

public interface ITenantContextSwitchRepository
{
    Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken);
    Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken);
    Task FinishSwitchAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId, long? tenantId, CancellationToken cancellationToken);
}

public sealed record TenantContextLogItem(long Id, long UsuarioGlobalId, long? TenantDestinoId, long? EntidadeDestinoId, string Motivo, DateTimeOffset IniciadoAt, DateTimeOffset? FinalizadoAt, string? Ip, string? UserAgent, Guid? CorrelationId);
