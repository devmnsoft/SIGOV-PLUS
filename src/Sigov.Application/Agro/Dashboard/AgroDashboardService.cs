using Microsoft.Extensions.Logging;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Dashboard;

public sealed class AgroDashboardService : IAgroDashboardService
{
    private readonly IAgroAccessChecker _accessChecker;
    private readonly IAgroDashboardRepository _repository;
    private readonly ILogger<AgroDashboardService> _logger;

    public AgroDashboardService(IAgroAccessChecker accessChecker, IAgroDashboardRepository repository, ILogger<AgroDashboardService> logger)
    {
        _accessChecker = accessChecker;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<AgroDashboardResponse>> ObterAsync(CancellationToken cancellationToken)
    {
        var context = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.DashboardVisualizar, "agro.dashboard"), cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return Result<AgroDashboardResponse>.Failure(context.Error!);
        }

        var value = context.Value!;
        _logger.LogInformation("Obtendo dashboard Agro para tenant {TenantId}.", value.TenantId);
        return Result<AgroDashboardResponse>.Success(await _repository.ObterAsync(value.TenantId, value.EntidadeId, cancellationToken).ConfigureAwait(false));
    }
}
