using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Produtores;

public sealed class AgroProdutorPermissionChecker
{
    private readonly IAgroAccessChecker _accessChecker;
    public AgroProdutorPermissionChecker(IAgroAccessChecker accessChecker) => _accessChecker = accessChecker;
    public Task<Result<AgroAccessContext>> CheckAsync(string permission, CancellationToken cancellationToken) => _accessChecker.CheckAsync(new AgroAccessRequest(permission, "agro.produtores"), cancellationToken);
}
