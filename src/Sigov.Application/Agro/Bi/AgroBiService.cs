using Microsoft.Extensions.Logging;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Bi;

public sealed class AgroBiService : IAgroBiService
{
    private readonly IAgroAccessChecker _accessChecker; private readonly IAgroBiRepository _repository; private readonly ILogger<AgroBiService> _logger;
    public AgroBiService(IAgroAccessChecker accessChecker, IAgroBiRepository repository, ILogger<AgroBiService> logger) { _accessChecker = accessChecker; _repository = repository; _logger = logger; }
    public async Task<Result<AgroBiDashboardResponse>> ObterDashboardAsync(CancellationToken cancellationToken)
    {
        var context = await _accessChecker.CheckAsync(new AgroAccessRequest(AgroPermissions.BiVisualizar, "agro.bi"), cancellationToken).ConfigureAwait(false); if (context.IsFailure) return Result<AgroBiDashboardResponse>.Failure(context.Error!);
        var value = context.Value!; _logger.LogInformation("Obtendo BI Agro tenant {TenantId}.", value.TenantId); return Result<AgroBiDashboardResponse>.Success(await _repository.ObterDashboardAsync(value.TenantId, value.EntidadeId, value.ExercicioId, cancellationToken).ConfigureAwait(false));
    }
}
