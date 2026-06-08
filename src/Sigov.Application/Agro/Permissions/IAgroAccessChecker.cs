using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Permissions;

public interface IAgroAccessChecker
{
    Task<Result<AgroAccessContext>> CheckAsync(AgroAccessRequest request, CancellationToken cancellationToken);
}

public sealed record AgroAccessRequest(string Permission, string? FeatureFlag = null, long? EntidadeId = null, long? ExercicioId = null);

public sealed record AgroAccessContext(long TenantId, long? EntidadeId, long? ExercicioId, long UsuarioId, IReadOnlyCollection<string> ProfileCodes, bool Global);
