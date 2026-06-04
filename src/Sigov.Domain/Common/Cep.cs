using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class Cep : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    private Cep(string value) => Value = value;

    public string Value { get; }

    public static Result<Cep> Create(string? value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (normalized.Length != 8 || normalized.Distinct().Count() == 1)
        {
            return Result<Cep>.Failure("CEP deve conter 8 dígitos válidos.");
        }

        return Result<Cep>.Success(new Cep(normalized));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
