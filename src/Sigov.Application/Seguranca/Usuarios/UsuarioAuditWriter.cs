namespace Sigov.Application.Seguranca.Usuarios;

public sealed class UsuarioAuditWriter
{
    public string BuildResourceKey(long usuarioId) => $"sigov.usuario:{usuarioId}";
}
