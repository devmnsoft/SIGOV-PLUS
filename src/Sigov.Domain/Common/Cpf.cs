using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class Cpf : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    private Cpf(string value) => Value = value;

    public string Value { get; }

    public static Result<Cpf> Create(string? value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (!IsValid(normalized))
        {
            return Result<Cpf>.Failure("CPF inválido.");
        }

        return Result<Cpf>.Success(new Cpf(normalized));
    }

    public static bool IsValid(string? value)
    {
        var cpf = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
        {
            return false;
        }

        var first = CalculateDigit(cpf, 9, 10);
        var second = CalculateDigit(cpf, 10, 11);
        return cpf[9] - '0' == first && cpf[10] - '0' == second;
    }

    private static int CalculateDigit(string digits, int length, int weight)
    {
        var sum = 0;
        for (var index = 0; index < length; index++)
        {
            sum += (digits[index] - '0') * (weight - index);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
