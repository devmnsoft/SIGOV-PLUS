namespace Sigov.Application.Saas.Context;

public interface IGlobalAdminChecker
{
    bool IsGlobalAdmin(IEnumerable<string> profileCodes);
    Task<bool> IsGlobalAdminAsync(long usuarioId, CancellationToken cancellationToken);
}
