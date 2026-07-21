namespace Sigov.Application.Operational;

public sealed record OperationalCommandContext(long TenantId, long UserId, string CorrelationId, string? IpAddress = null, string? UserAgent = null);

public sealed record TarefaDto(long Id, long TenantId, string Titulo, string Status, string Prioridade, long? ResponsavelId, DateTimeOffset? PrazoEm, DateTimeOffset CreatedAt, long Version = 0);
public sealed record CriarTarefaRequest(string Titulo, string? Descricao, string Prioridade, long? ResponsavelId, DateTimeOffset? PrazoEm);
public sealed record AlterarStatusTarefaRequest(long TarefaId, string NovoStatus, string? Comentario, long Version = 0);
public sealed record AtualizarTarefaRequest(long TarefaId, string Titulo, string? Descricao, string Prioridade, long? ResponsavelId, DateTimeOffset? PrazoEm, long Version);
public sealed record AtribuirTarefaRequest(long TarefaId, long ResponsavelId, long Version);
public sealed record DelegarTarefaRequest(long TarefaId, long NovoResponsavelId, string? Comentario, long Version);
public sealed record ComentarioTarefaRequest(long TarefaId, string Comentario);
public sealed record VinculoTarefaRequest(long TarefaId, string Tipo, long EntidadeId);
public sealed record TarefaHistoricoDto(long Id, long TarefaId, string Acao, DateTimeOffset CreatedAt);

public sealed record AgendaCompromissoDto(long Id, long TenantId, string Titulo, DateTimeOffset InicioEm, DateTimeOffset FimEm, string Status);
public sealed record CriarCompromissoRequest(string Titulo, string? Descricao, DateTimeOffset InicioEm, DateTimeOffset FimEm, IReadOnlyCollection<long> Participantes);
public sealed record PrazoOperacionalDto(long Id, long TenantId, string Titulo, DateTimeOffset VenceEm, string Status, long? TarefaId);
public sealed record NotificacaoDto(long Id, long TenantId, long UsuarioId, string Tipo, string Titulo, bool Lida, DateTimeOffset CreatedAt);
public sealed record KanbanCardDto(long Id, long TenantId, string Origem, long EntidadeId, string Titulo, string Coluna, int Ordem, long? ResponsavelId, DateTimeOffset? PrazoEm);
public sealed record OperationalEvent(string EventType, string AggregateType, string AggregateId, long TenantId, long UserId, string CorrelationId, object Payload, string IdempotencyKey);

public interface ITarefaRepository
{
    Task<TarefaDto> CriarAsync(CriarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto?> ObterAsync(long tenantId, long tarefaId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TarefaDto>> ListarAsync(long tenantId, long? responsavelId, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<TarefaDto> AlterarStatusAsync(AlterarStatusTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> AtualizarAsync(AtualizarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> AtribuirAsync(AtribuirTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> DelegarAsync(DelegarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> IniciarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> PausarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> ConcluirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> ReabrirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> CancelarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task AdicionarComentarioAsync(ComentarioTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task AdicionarVinculoAsync(VinculoTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<TarefaHistoricoDto>> ListarHistoricoAsync(long tenantId, long tarefaId, CancellationToken cancellationToken);
}

public interface ITarefaHistoricoRepository
{
    Task RegistrarAsync(long tarefaId, string acao, object? antes, object? depois, OperationalCommandContext context, CancellationToken cancellationToken);
}

public interface ITarefaNotificationService
{
    Task NotificarAsync(long tarefaId, string tipo, OperationalCommandContext context, CancellationToken cancellationToken);
}

public interface ITarefaService
{
    Task<TarefaDto> CriarAsync(CriarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto?> ObterAsync(long tenantId, long tarefaId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TarefaDto>> ListarAsync(long tenantId, long? responsavelId, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<TarefaDto> AtualizarAsync(AtualizarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> AtribuirAsync(AtribuirTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> DelegarAsync(DelegarTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> AlterarStatusAsync(AlterarStatusTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> IniciarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> PausarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> ConcluirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> ReabrirAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<TarefaDto> CancelarAsync(long tarefaId, long version, OperationalCommandContext context, CancellationToken cancellationToken);
    Task AdicionarComentarioAsync(ComentarioTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task AdicionarVinculoAsync(VinculoTarefaRequest request, OperationalCommandContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<TarefaHistoricoDto>> ListarHistoricoAsync(long tenantId, long tarefaId, CancellationToken cancellationToken);
}
public interface IAgendaRepository { Task<AgendaCompromissoDto> CriarAsync(CriarCompromissoRequest request, OperationalCommandContext context, CancellationToken cancellationToken); }
public interface IAgendaService { Task<AgendaCompromissoDto> CriarAsync(CriarCompromissoRequest request, OperationalCommandContext context, CancellationToken cancellationToken); }
public interface IPrazoOperacionalRepository { Task<IReadOnlyList<PrazoOperacionalDto>> ListarVencidosAsync(long tenantId, DateTimeOffset referencia, CancellationToken cancellationToken); }
public interface IPrazoOperacionalService { Task<IReadOnlyList<PrazoOperacionalDto>> ListarVencidosAsync(long tenantId, DateTimeOffset referencia, CancellationToken cancellationToken); }
public interface INotificacaoRepository { Task<IReadOnlyList<NotificacaoDto>> ListarAsync(long tenantId, long usuarioId, bool? lida, CancellationToken cancellationToken); Task MarcarLidaAsync(long tenantId, long usuarioId, long notificacaoId, string correlationId, CancellationToken cancellationToken); }
public interface INotificacaoService { Task<IReadOnlyList<NotificacaoDto>> ListarAsync(long tenantId, long usuarioId, bool? lida, CancellationToken cancellationToken); Task MarcarLidaAsync(long tenantId, long usuarioId, long notificacaoId, string correlationId, CancellationToken cancellationToken); }
public interface INotificacaoPreferenceService { Task SalvarAsync(long tenantId, long usuarioId, string tipo, bool habilitada, CancellationToken cancellationToken); }
public interface IKanbanRepository { Task<IReadOnlyList<KanbanCardDto>> ListarAsync(long tenantId, string origem, long? responsavelId, string? sla, CancellationToken cancellationToken); Task MoverAsync(long tenantId, long cardId, string coluna, int ordem, OperationalCommandContext context, CancellationToken cancellationToken); }
public interface IKanbanService { Task<IReadOnlyList<KanbanCardDto>> ListarAsync(long tenantId, string origem, long? responsavelId, string? sla, CancellationToken cancellationToken); Task MoverAsync(long tenantId, long cardId, string coluna, int ordem, OperationalCommandContext context, CancellationToken cancellationToken); }
public interface IOperationalEventPublisher { Task PublishAsync(OperationalEvent operationalEvent, CancellationToken cancellationToken); }
