namespace Sigov.Application.Enterprise;

public interface IEnterpriseCrudService
{
    Task<IReadOnlyList<EnterpriseListItem>> ListAsync(string area, Guid tenantId, int page = 1, int pageSize = 50, string? search = null, CancellationToken cancellationToken = default);
    Task<EnterpriseListItem?> GetByIdAsync(string area, Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<EnterpriseActionResult> CreateAsync(string area, EnterpriseMutationRequest request, Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task<EnterpriseActionResult> UpdateAsync(string area, Guid id, EnterpriseMutationRequest request, Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task<EnterpriseActionResult> DeleteAsync(string area, Guid id, Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task<EnterpriseActionResult> RestoreAsync(string area, Guid id, Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task<EnterpriseActionResult> ExecuteActionAsync(string area, Guid id, string action, Guid tenantId, string correlationId, CancellationToken cancellationToken = default);
    Task<EnterpriseDashboard> DashboardAsync(string module, Guid tenantId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportCsvAsync(string area, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnterpriseListItem>> SearchAsync(string query, Guid tenantId, CancellationToken cancellationToken = default);
}
