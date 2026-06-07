namespace Sigov.Application.Seguranca.Usuarios;

public sealed class UsuarioPermissionChecker
{
    public bool CanManageUsers(IEnumerable<string> permissions) => permissions.Contains("seguranca.usuarios.editar", StringComparer.OrdinalIgnoreCase);
}
