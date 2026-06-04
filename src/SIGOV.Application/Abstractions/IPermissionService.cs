namespace SIGOV.Application.Abstractions;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(long usuarioId, string modulo, string recurso, string acao, CancellationToken cancellationToken = default);
}
