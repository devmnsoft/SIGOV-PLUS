namespace Sigov.Application.Saas.Context;

public interface IOperationalContextService
{
    Task<OperationalContext?> CurrentAsync(long userId, string sessionHash, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ContextOption>> SearchTenantsAsync(long userId, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ContextOption>> OptionsAsync(long userId, long tenantId, ContextOptionType type, CancellationToken cancellationToken);
    Task<ContextValidation> ValidateAsync(long userId, ContextSelection selection, CancellationToken cancellationToken);
    Task<OperationalContext> SelectAsync(ContextChange change, CancellationToken cancellationToken);
    Task<OperationalContext> ReturnGlobalAsync(ContextChange change, CancellationToken cancellationToken);
    Task EndAsync(long userId, string sessionHash, string correlationId, string? ip, string? userAgent, CancellationToken cancellationToken);
}

public sealed class OperationalContextService(ITenantContextSwitchRepository repository) : IOperationalContextService
{
    public Task<OperationalContext?> CurrentAsync(long userId, string sessionHash, CancellationToken ct) => repository.GetCurrentAsync(userId, sessionHash, ct);
    public Task<IReadOnlyCollection<ContextOption>> SearchTenantsAsync(long userId, string? search, int page, int pageSize, CancellationToken ct)
        => repository.SearchTenantsAsync(userId, search?.Trim(), (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 50), Math.Clamp(pageSize, 1, 50), ct);
    public Task<IReadOnlyCollection<ContextOption>> OptionsAsync(long userId, long tenantId, ContextOptionType type, CancellationToken ct) => repository.GetOptionsAsync(userId, tenantId, type, ct);
    public Task<ContextValidation> ValidateAsync(long userId, ContextSelection selection, CancellationToken ct) => repository.ValidateAsync(userId, selection, ct);

    public async Task<OperationalContext> SelectAsync(ContextChange change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change.Selection);
        var validation = await repository.ValidateAsync(change.UsuarioId, change.Selection, ct).ConfigureAwait(false);
        if (!validation.Valid)
        {
            await repository.RecordDeniedAsync(change, validation.Code ?? "CONTEXTO_NEGADO", ct).ConfigureAwait(false);
            throw new InvalidOperationException($"{validation.Code}:{validation.Message}");
        }
        if (validation.RequiresJustification && (string.IsNullOrWhiteSpace(change.Selection.Justificativa) || change.Selection.Justificativa.Trim().Length < 15))
        {
            await repository.RecordDeniedAsync(change, "JUSTIFICATIVA_OBRIGATORIA", ct).ConfigureAwait(false);
            throw new ArgumentException("A justificativa deve possuir ao menos 15 caracteres.", nameof(change));
        }
        return await repository.ChangeAsync(change, ct).ConfigureAwait(false);
    }

    public Task<OperationalContext> ReturnGlobalAsync(ContextChange change, CancellationToken ct) => repository.ReturnGlobalAsync(change, ct);
    public Task EndAsync(long userId, string sessionHash, string correlationId, string? ip, string? userAgent, CancellationToken ct) => repository.EndSessionAsync(userId, sessionHash, correlationId, ip, userAgent, ct);
}
