using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class Cnpj : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);
    private static readonly int[] FirstWeights = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] SecondWeights = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    private Cnpj(string value) => Value = value;

    public string Value { get; }

    public static Result<Cnpj> Create(string? value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (!IsValid(normalized))
        {
            return Result<Cnpj>.Failure("CNPJ inválido.");
        }

        return Result<Cnpj>.Success(new Cnpj(normalized));
    }

    public static bool IsValid(string? value)
    {
        var cnpj = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1)
        {
            return false;
        }

        var first = CalculateDigit(cnpj, FirstWeights);
        var second = CalculateDigit(cnpj, SecondWeights);
        return cnpj[12] - '0' == first && cnpj[13] - '0' == second;
    }

    private static int CalculateDigit(string digits, IReadOnlyList<int> weights)
    {
        var sum = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            sum += (digits[index] - '0') * weights[index];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
