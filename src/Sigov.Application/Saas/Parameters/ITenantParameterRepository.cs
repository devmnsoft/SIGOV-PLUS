namespace Sigov.Application.Saas.Parameters;

public interface ITenantParameterRepository
{
    Task<IReadOnlyCollection<TenantParameterDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<TenantParameterDefinitionDto?> GetDefinitionAsync(string codigo, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantParameterValueDto>> GetValuesAsync(string codigo, TenantParameterResolveContext context, CancellationToken cancellationToken);
    Task UpsertValueAsync(string codigo, TenantParameterValueDto value, long? userId, Guid? correlationId, CancellationToken cancellationToken);
}
