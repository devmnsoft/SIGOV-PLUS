using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class CpfCnpj : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    private CpfCnpj(string value) => Value = value;

    public string Value { get; }

    public static Result<CpfCnpj> Create(string value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        return normalized.Length is 11 or 14
            ? Result<CpfCnpj>.Success(new CpfCnpj(normalized))
            : Result<CpfCnpj>.Failure("CPF/CNPJ deve conter 11 ou 14 dígitos.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
