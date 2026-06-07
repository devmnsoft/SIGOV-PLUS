namespace Sigov.Application.Seguranca.Usuarios;

public sealed class UsuarioValidator
{
    public bool IsLoginValido(string? login) => !string.IsNullOrWhiteSpace(login) && login.Length <= 120;
}
