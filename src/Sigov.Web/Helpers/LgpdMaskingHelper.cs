namespace Sigov.Web.Helpers;

public static class LgpdMaskingHelper
{
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal)) return "***";
        var parts = email.Trim().Split('@', 2);
        var name = parts[0].Length <= 1 ? "*" : $"{parts[0][0]}***";
        return $"{name}@{parts[1]}";
    }

    public static string MaskDocument(string? documento)
    {
        var digits = OnlyDigits(documento);
        if (digits.Length < 5) return "***";
        return $"{digits[..2]}***{digits[^2..]}";
    }

    public static string MaskPhone(string? telefone)
    {
        var digits = OnlyDigits(telefone);
        return digits.Length < 4 ? "***" : $"***{digits[^4..]}";
    }

    public static string MaskName(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "***";
        var parts = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length <= 2 ? "***" : $"{parts[0][0]}***";
        return $"{parts[0][0]}*** {parts[^1][0]}***";
    }

    private static string OnlyDigits(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}
