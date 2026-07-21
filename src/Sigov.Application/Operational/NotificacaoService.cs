namespace Sigov.Application.Operational;

public sealed class NotificacaoService : INotificacaoService
{
    private readonly INotificacaoRepository _repository;

    public NotificacaoService(INotificacaoRepository repository) => _repository = repository;

    public Task<IReadOnlyList<NotificacaoDto>> ListarAsync(long tenantId, long usuarioId, bool? lida, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        if (usuarioId <= 0) throw new ArgumentOutOfRangeException(nameof(usuarioId));
        return _repository.ListarAsync(tenantId, usuarioId, lida, cancellationToken);
    }

    public Task MarcarLidaAsync(long tenantId, long usuarioId, long notificacaoId, string correlationId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        if (usuarioId <= 0) throw new ArgumentOutOfRangeException(nameof(usuarioId));
        if (notificacaoId <= 0) throw new ArgumentOutOfRangeException(nameof(notificacaoId));
        return _repository.MarcarLidaAsync(tenantId, usuarioId, notificacaoId, correlationId, cancellationToken);
    }
}
