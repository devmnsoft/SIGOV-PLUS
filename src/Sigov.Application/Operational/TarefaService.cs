namespace Sigov.Application.Operational;

public sealed class TarefaService : ITarefaService
{
    private static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["ABERTA"] = new[] { "ATRIBUIDA", "EM_ANDAMENTO", "CANCELADA" },
        ["ATRIBUIDA"] = new[] { "EM_ANDAMENTO", "CANCELADA" },
        ["EM_ANDAMENTO"] = new[] { "AGUARDANDO", "PAUSADA", "CONCLUIDA", "CANCELADA" },
        ["AGUARDANDO"] = new[] { "EM_ANDAMENTO", "CANCELADA" },
        ["PAUSADA"] = new[] { "EM_ANDAMENTO", "CANCELADA" },
        ["CONCLUIDA"] = new[] { "REABERTA" },
        ["CANCELADA"] = Array.Empty<string>(),
        ["VENCIDA"] = new[] { "EM_ANDAMENTO", "CANCELADA" },
        ["REABERTA"] = new[] { "EM_ANDAMENTO", "CANCELADA" }
    };

    private readonly ITarefaRepository _repository;
    private readonly ITarefaHistoricoRepository _historico;
    private readonly ITarefaNotificationService _notifications;
    private readonly IOperationalEventPublisher _events;

    public TarefaService(ITarefaRepository repository, ITarefaHistoricoRepository historico, ITarefaNotificationService notifications, IOperationalEventPublisher events)
    { _repository = repository; _historico = historico; _notifications = notifications; _events = events; }

    public async Task<TarefaDto> CriarAsync(CriarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        var tarefa = await _repository.CriarAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _historico.RegistrarAsync(tarefa.Id, "tarefa.criada", null, tarefa, context, cancellationToken).ConfigureAwait(false);
        await _events.PublishAsync(new OperationalEvent("tarefa.criada", "tarefa", tarefa.Id.ToString(), context.TenantId, context.UserId, context.CorrelationId, new { tarefa.Id, tarefa.Status }, $"tarefa:{tarefa.Id}:criada"), cancellationToken).ConfigureAwait(false);
        await _notifications.NotificarAsync(tarefa.Id, "tarefa.criada", context, cancellationToken).ConfigureAwait(false);
        return tarefa;
    }

    public Task<TarefaDto?> ObterAsync(long tenantId, long tarefaId, CancellationToken cancellationToken) => _repository.ObterAsync(tenantId, tarefaId, cancellationToken);
    public Task<IReadOnlyList<TarefaDto>> ListarAsync(long tenantId, long? responsavelId, string? status, int page, int pageSize, CancellationToken cancellationToken) => _repository.ListarAsync(tenantId, responsavelId, status, page, pageSize, cancellationToken);

    public async Task<TarefaDto> AlterarStatusAsync(AlterarStatusTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        var atual = await _repository.ObterAsync(context.TenantId, request.TarefaId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Tarefa não encontrada no tenant informado.");
        if (!Transitions.TryGetValue(atual.Status, out var allowed) || !allowed.Contains(request.NovoStatus, StringComparer.OrdinalIgnoreCase))
        { throw new InvalidOperationException($"Transição inválida de {atual.Status} para {request.NovoStatus}."); }
        var alterada = await _repository.AlterarStatusAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _historico.RegistrarAsync(alterada.Id, "tarefa.status.alterado", atual, alterada, context, cancellationToken).ConfigureAwait(false);
        await _events.PublishAsync(new OperationalEvent($"tarefa.{request.NovoStatus.ToLowerInvariant()}", "tarefa", alterada.Id.ToString(), context.TenantId, context.UserId, context.CorrelationId, new { alterada.Id, alterada.Status }, $"tarefa:{alterada.Id}:status:{alterada.Status}:{context.CorrelationId}"), cancellationToken).ConfigureAwait(false);
        return alterada;
    }

    public async Task<TarefaDto> AtualizarAsync(AtualizarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        var atual = await ObterObrigatoriaAsync(context.TenantId, request.TarefaId, cancellationToken).ConfigureAwait(false);
        var alterada = await _repository.AtualizarAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _historico.RegistrarAsync(alterada.Id, "tarefa.atualizada", atual, alterada, context, cancellationToken).ConfigureAwait(false);
        return alterada;
    }

    public async Task<TarefaDto> AtribuirAsync(AtribuirTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        var atual = await ObterObrigatoriaAsync(context.TenantId, request.TarefaId, cancellationToken).ConfigureAwait(false);
        var alterada = await _repository.AtribuirAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _historico.RegistrarAsync(alterada.Id, "tarefa.atribuida", atual, alterada, context, cancellationToken).ConfigureAwait(false);
        await _notifications.NotificarAsync(alterada.Id, "tarefa.atribuida", context, cancellationToken).ConfigureAwait(false);
        return alterada;
    }

    public async Task<TarefaDto> DelegarAsync(DelegarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        var atual = await ObterObrigatoriaAsync(context.TenantId, request.TarefaId, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(atual.Status, TarefaStatus.ATRIBUIDA.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Delegação é uma ação permitida somente para tarefas atribuídas; ela não cria status DELEGADA.");
        }
        var alterada = await _repository.DelegarAsync(request, context, cancellationToken).ConfigureAwait(false);
        await _historico.RegistrarAsync(alterada.Id, "tarefa.delegada", atual, alterada, context, cancellationToken).ConfigureAwait(false);
        await _notifications.NotificarAsync(alterada.Id, "tarefa.delegada", context, cancellationToken).ConfigureAwait(false);
        return alterada;
    }

    public Task<TarefaDto> IniciarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, TarefaStatus.EM_ANDAMENTO.ToString(), null, version), context, cancellationToken);
    public Task<TarefaDto> PausarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, TarefaStatus.PAUSADA.ToString(), null, version), context, cancellationToken);
    public Task<TarefaDto> ConcluirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, TarefaStatus.CONCLUIDA.ToString(), null, version), context, cancellationToken);
    public Task<TarefaDto> ReabrirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, TarefaStatus.REABERTA.ToString(), null, version), context, cancellationToken);
    public Task<TarefaDto> CancelarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken) => AlterarStatusAsync(new AlterarStatusTarefaRequest(tarefaId, TarefaStatus.CANCELADA.ToString(), null, version), context, cancellationToken);
    public Task AdicionarComentarioAsync(ComentarioTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken) => _repository.AdicionarComentarioAsync(request, context, cancellationToken);
    public Task AdicionarVinculoAsync(VinculoTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken) => _repository.AdicionarVinculoAsync(request, context, cancellationToken);
    public Task<IReadOnlyList<TarefaHistoricoDto>> ListarHistoricoAsync(long tenantId, long tarefaId, CancellationToken cancellationToken) => _repository.ListarHistoricoAsync(tenantId, tarefaId, cancellationToken);

    private async Task<TarefaDto> ObterObrigatoriaAsync(long tenantId, long tarefaId, CancellationToken cancellationToken)
        => await _repository.ObterAsync(tenantId, tarefaId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Tarefa não encontrada no tenant informado.");

}
