namespace Sigov.Application.Saas.Context;

public interface ITenantContextSwitchRepository
{
    // Mantido para compatibilidade binária; a implementação consulta a permissão global persistida.
    Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken);
    Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken);
    Task FinishSwitchAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId, long? tenantId, CancellationToken cancellationToken);

    Task<OperationalContext?> GetCurrentAsync(long usuarioId, string sessionHash, CancellationToken cancellationToken)
        => Task.FromResult<OperationalContext?>(null);
    Task<IReadOnlyCollection<ContextOption>> SearchTenantsAsync(long usuarioId, string? search, int offset, int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ContextOption>>(Array.Empty<ContextOption>());
    Task<IReadOnlyCollection<ContextOption>> GetOptionsAsync(long usuarioId, long tenantId, ContextOptionType type, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ContextOption>>(Array.Empty<ContextOption>());
    Task<ContextValidation> ValidateAsync(long usuarioId, ContextSelection selection, CancellationToken cancellationToken)
        => Task.FromResult(new ContextValidation(false, false, null, "contexto_indisponivel", "Contexto indisponível."));
    Task<OperationalContext> ChangeAsync(ContextChange change, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    Task<OperationalContext> ReturnGlobalAsync(ContextChange change, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    Task EndSessionAsync(long usuarioId, string sessionHash, string correlationId, string? ip, string? userAgent, CancellationToken cancellationToken)
        => Task.CompletedTask;
    Task RecordDeniedAsync(ContextChange change, string code, CancellationToken cancellationToken) => Task.CompletedTask;
}

public enum ContextOptionType { Unidade, Exercicio, Sistema }
public sealed class ContextOption
{
    public long Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Situacao { get; set; }
    public string? RotaInicial { get; set; }
    public string? Icone { get; set; }
    public long? ParentId { get; set; }
}
public sealed record ContextSelection(long TenantId, long EntidadeId, long UnidadeId, long ExercicioId, long SistemaId, string ModoAcesso, string? Justificativa, long? Versao);
public sealed record ContextValidation(bool Valid, bool RequiresJustification, long? PerfilId, string? Code, string Message);
public sealed record ContextChange(long UsuarioId, string SessionHash, ContextSelection? Selection, string CorrelationId, string? Ip, string? UserAgent, DateTimeOffset ExpiresAt);
public sealed record OperationalContext(long SessionId, long UsuarioId, long? TenantId, long? EntidadeId, long? UnidadeId, long? ExercicioId, long? SistemaId, long? PerfilId, string ModoAcesso, bool IsGlobal, long Versao, DateTimeOffset ExpiresAt, string? EmpresaNome = null, string? UnidadeNome = null, string? ExercicioNome = null, string? SistemaNome = null, string? RotaInicial = null);
public sealed record TenantContextLogItem(long Id, long UsuarioGlobalId, long? TenantDestinoId, long? EntidadeDestinoId, string Motivo, DateTimeOffset IniciadoAt, DateTimeOffset? FinalizadoAt, string? Ip, string? UserAgent, Guid? CorrelationId);
