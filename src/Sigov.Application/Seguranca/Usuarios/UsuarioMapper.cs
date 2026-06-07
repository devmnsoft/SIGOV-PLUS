namespace Sigov.Application.Seguranca.Usuarios;

public sealed class UsuarioMapper
{
    public string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 1 ? "***" : string.Concat(email[0], "***", email[at..]);
    }
}
