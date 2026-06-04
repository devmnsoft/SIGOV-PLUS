using System.Text.RegularExpressions;
using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Security;

public sealed class LgpdMaskingService : ILgpdMaskingService
{
    public string Mask(string? value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return dataType.ToUpperInvariant() switch
        {
            "CPF" => Regex.Replace(value, "(\\d{3})\\d{6}(\\d{2})", "$1******$2"),
            "CNPJ" => Regex.Replace(value, "(\\d{2})\\d{9}(\\d{3})", "$1*********$2"),
            "EMAIL" => MaskEmail(value),
            "TELEFONE" => Regex.Replace(value, "\\d(?=\\d{4})", "*"),
            _ => "***"
        };
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        return parts.Length == 2 ? $"{parts[0][0]}***@{parts[1]}" : "***";
    }
}
