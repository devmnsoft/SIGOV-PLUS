using System.Security.Cryptography;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasSolicitacaoClienteMapper
{
    public static string NewProtocol() => $"SIGOV-{DateTimeOffset.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 999999)}";
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal)) return email;
        var parts = email.Split('@', 2);
        return $"{parts[0][0]}***@{parts[1]}";
    }
    public static string? MaskDocument(string? document) => string.IsNullOrWhiteSpace(document) || document.Length < 4 ? document : $"***{document[^4..]}";
}
