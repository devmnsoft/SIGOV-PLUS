namespace Sigov.Application.Parameters;

public sealed class ModuleParameterService : IModuleParameterService
{
    private static readonly HashSet<string> Modules = new(StringComparer.OrdinalIgnoreCase) { "EDUCACAO", "RH", "FOLHA", "PORTAL_SERVIDOR", "PORTAL_EDUCACAO" };
    private readonly IModuleParameterRepository _repository;

    public ModuleParameterService(IModuleParameterRepository repository) => _repository = repository;

    public Task<IReadOnlyCollection<ModuleParameterValue>> ListAsync(long tenantId, string module, CancellationToken cancellationToken)
        => _repository.ListAsync(RequireTenant(tenantId), NormalizeModule(module), cancellationToken);

    public Task<IReadOnlyCollection<ModuleParameterHistory>> HistoryAsync(long tenantId, string module, int page, int pageSize, CancellationToken cancellationToken)
        => _repository.HistoryAsync(RequireTenant(tenantId), NormalizeModule(module), Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public Task SaveAsync(long tenantId, string module, SaveModuleParametersRequest request, long? userId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Values.Count == 0) throw new ArgumentException("Informe ao menos um parâmetro.", nameof(request));
        if (request.Values.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            throw new ArgumentException("Código e valor são obrigatórios.", nameof(request));
        return _repository.SaveAsync(RequireTenant(tenantId), NormalizeModule(module), request.Values, userId, correlationId, cancellationToken);
    }

    private static long RequireTenant(long tenantId) => tenantId > 0 ? tenantId : throw new InvalidOperationException("Tenant obrigatório.");
    private static string NormalizeModule(string module)
    {
        var normalized = module?.Trim().ToUpperInvariant() ?? string.Empty;
        return Modules.Contains(normalized) ? normalized : throw new ArgumentException("Módulo inválido.", nameof(module));
    }
}
