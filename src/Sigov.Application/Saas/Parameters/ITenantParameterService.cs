namespace Sigov.Application.Saas.Parameters;

public interface ITenantParameterService
{
    Task<IReadOnlyCollection<TenantParameterDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<TenantParameterDefinitionDto?> GetDefinitionAsync(string codigo, CancellationToken cancellationToken);
    Task SaveValueAsync(string codigo, TenantParameterValueDto value, long? userId, Guid? correlationId, CancellationToken cancellationToken);
}
