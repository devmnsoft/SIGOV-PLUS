namespace Sigov.Application.Workflows;

public sealed record WorkflowTarefaDto(long Id, long InstanciaId, string Modulo, string TipoFluxo,
    string ReferenciaTipo, long ReferenciaId, string Status, string EtapaAtual, long? ResponsavelId,
    string? GrupoResponsavel, DateTimeOffset? Prazo, string Prioridade, string Dados, DateTimeOffset CreatedAt);

public sealed record WorkflowHistoricoDto(long Id, string Decisao, string EtapaAnterior, string EtapaNova,
    string? Justificativa, long? UsuarioId, DateTimeOffset CreatedAt, string CorrelationId);

public sealed record WorkflowDecisaoRequest(string? Justificativa, long? ResponsavelId, string? GrupoResponsavel);

public interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowTarefaDto>> ListarTarefasAsync(long tenantId, long? responsavelId, string? modulo, string? status, CancellationToken ct);
    Task<IReadOnlyList<WorkflowHistoricoDto>> ListarHistoricoAsync(long tenantId, long instanciaId, CancellationToken ct);
    Task<bool> DecidirAsync(long tenantId, long instanciaId, long? usuarioId, string decisao, string? justificativa,
        long? responsavelId, string? grupoResponsavel, string correlationId, CancellationToken ct);
}

public interface IWorkflowOperacionalService
{
    Task<IReadOnlyList<WorkflowTarefaDto>> ListarTarefasAsync(long tenantId, long? responsavelId, string? modulo, string? status, CancellationToken ct);
    Task<IReadOnlyList<WorkflowHistoricoDto>> ListarHistoricoAsync(long tenantId, long instanciaId, CancellationToken ct);
    Task<bool> DecidirAsync(long tenantId, long instanciaId, long? usuarioId, string decisao, WorkflowDecisaoRequest request, string correlationId, CancellationToken ct);
}

public sealed class WorkflowOperacionalService : IWorkflowOperacionalService
{
    private static readonly HashSet<string> Decisoes = new(StringComparer.OrdinalIgnoreCase) { "APROVAR", "REPROVAR", "ENCAMINHAR", "CANCELAR" };
    private readonly IWorkflowRepository _repository;
    public WorkflowOperacionalService(IWorkflowRepository repository) => _repository = repository;

    public Task<IReadOnlyList<WorkflowTarefaDto>> ListarTarefasAsync(long tenantId, long? responsavelId, string? modulo, string? status, CancellationToken ct)
        => tenantId > 0 ? _repository.ListarTarefasAsync(tenantId, responsavelId, modulo, status, ct) : throw new ArgumentOutOfRangeException(nameof(tenantId));

    public Task<IReadOnlyList<WorkflowHistoricoDto>> ListarHistoricoAsync(long tenantId, long instanciaId, CancellationToken ct)
        => tenantId > 0 && instanciaId > 0 ? _repository.ListarHistoricoAsync(tenantId, instanciaId, ct) : throw new ArgumentOutOfRangeException(nameof(instanciaId));

    public Task<bool> DecidirAsync(long tenantId, long instanciaId, long? usuarioId, string decisao, WorkflowDecisaoRequest request, string correlationId, CancellationToken ct)
    {
        if (tenantId <= 0 || instanciaId <= 0) throw new ArgumentOutOfRangeException(nameof(instanciaId));
        if (!Decisoes.Contains(decisao)) throw new ArgumentException("Decisão inválida.", nameof(decisao));
        if (decisao is "REPROVAR" or "CANCELAR" && string.IsNullOrWhiteSpace(request.Justificativa))
            throw new ArgumentException("Justificativa é obrigatória para reprovação ou cancelamento.", nameof(request));
        if (decisao == "ENCAMINHAR" && request.ResponsavelId is null && string.IsNullOrWhiteSpace(request.GrupoResponsavel))
            throw new ArgumentException("Informe o responsável ou grupo de destino.", nameof(request));
        return _repository.DecidirAsync(tenantId, instanciaId, usuarioId, decisao, request.Justificativa?.Trim(), request.ResponsavelId,
            request.GrupoResponsavel?.Trim(), correlationId, ct);
    }
}
