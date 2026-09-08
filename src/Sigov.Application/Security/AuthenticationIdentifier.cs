using System.Net.Mail;

namespace Sigov.Application.Security;

public enum AuthenticationIdentifierKind
{
    Invalid,
    LegacyLogin,
    Email,
    Cpf,
    Cnpj
}

public sealed record AuthenticationIdentifier(AuthenticationIdentifierKind Kind, string Value, bool IsValid)
{
    public static AuthenticationIdentifier Invalid() => new(AuthenticationIdentifierKind.Invalid, string.Empty, false);
}

/// <summary>Normaliza o identificador antes de qualquer consulta e valida CPF/CNPJ pelos dígitos verificadores.</summary>
public static class AuthenticationIdentifierNormalizer
{
    public static AuthenticationIdentifier Normalize(string? input)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length is 0 or > 254) return AuthenticationIdentifier.Invalid();

        if (value.Contains('@'))
        {
            var normalizedEmail = value.ToLowerInvariant();
            return MailAddress.TryCreate(normalizedEmail, out var address) &&
                   string.Equals(address.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase)
                ? new(AuthenticationIdentifierKind.Email, normalizedEmail, true)
                : AuthenticationIdentifier.Invalid();
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && value.All(character => char.IsDigit(character) || character is '.' or '-'))
            return new(AuthenticationIdentifierKind.Cpf, digits, IsValidCpf(digits));
        if (digits.Length == 14 && value.All(character => char.IsDigit(character) || character is '.' or '-' or '/'))
            return new(AuthenticationIdentifierKind.Cnpj, digits, IsValidCnpj(digits));

        return value.Length is >= 3 and <= 100
            ? new(AuthenticationIdentifierKind.LegacyLogin, value.ToLowerInvariant(), true)
            : AuthenticationIdentifier.Invalid();
    }

    private static bool IsValidCpf(string value)
    {
        if (value.Distinct().Count() == 1) return false;
        var first = Digit(value, 9, 10);
        var second = Digit(value, 10, 11);
        return value[9] - '0' == first && value[10] - '0' == second;

        static int Digit(string cpf, int length, int initialWeight)
        {
            var sum = 0;
            for (var index = 0; index < length; index++) sum += (cpf[index] - '0') * (initialWeight - index);
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }
    }

    private static bool IsValidCnpj(string value)
    {
        if (value.Distinct().Count() == 1) return false;
        var first = Digit(value, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = Digit(value, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return value[12] - '0' == first && value[13] - '0' == second;

        static int Digit(string cnpj, int length, int[] weights)
        {
            var sum = 0;
            for (var index = 0; index < length; index++) sum += (cnpj[index] - '0') * weights[index];
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }
    }
}
