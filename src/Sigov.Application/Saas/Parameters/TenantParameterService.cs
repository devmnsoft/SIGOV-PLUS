namespace Sigov.Application.Saas.Parameters;

public sealed class TenantParameterService : ITenantParameterService
{
    private readonly ITenantParameterRepository _repository;

    public TenantParameterService(ITenantParameterRepository repository) => _repository = repository;

    public Task<IReadOnlyCollection<TenantParameterDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken) => _repository.GetDefinitionsAsync(cancellationToken);

    public Task<TenantParameterDefinitionDto?> GetDefinitionAsync(string codigo, CancellationToken cancellationToken) => _repository.GetDefinitionAsync(codigo, cancellationToken);

    public Task SaveValueAsync(string codigo, TenantParameterValueDto value, long? userId, Guid? correlationId, CancellationToken cancellationToken) => _repository.UpsertValueAsync(codigo, value, userId, correlationId, cancellationToken);
}
