using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Dashboard;

public interface IAgroDashboardService
{
    Task<Result<AgroDashboardResponse>> ObterAsync(CancellationToken cancellationToken);
}
