namespace Sigov.Application.Operational;

public sealed class KanbanService : IKanbanService
{
    private readonly IKanbanRepository _repository;

    public KanbanService(IKanbanRepository repository) => _repository = repository;

    public Task<IReadOnlyList<KanbanCardDto>> ListarAsync(long tenantId, string origem, long? responsavelId, string? sla, CancellationToken cancellationToken)
    {
        if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
        if (string.IsNullOrWhiteSpace(origem)) throw new ArgumentException("Origem é obrigatória.", nameof(origem));
        return _repository.ListarAsync(tenantId, origem, responsavelId, sla, cancellationToken);
    }

    public Task MoverAsync(long tenantId, long cardId, string coluna, int ordem, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        if (tenantId != context.TenantId) throw new InvalidOperationException("Tenant do comando diverge do tenant do cartão.");
        if (cardId <= 0) throw new ArgumentOutOfRangeException(nameof(cardId));
        if (string.IsNullOrWhiteSpace(coluna)) throw new ArgumentException("Coluna é obrigatória.", nameof(coluna));
        return _repository.MoverAsync(tenantId, cardId, coluna, ordem, context, cancellationToken);
    }
}
