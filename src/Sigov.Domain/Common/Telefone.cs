using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class Telefone : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    private Telefone(string value) => Value = value;

    public string Value { get; }

    public static Result<Telefone> Create(string? value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (normalized.Length is < 10 or > 11 || normalized.Distinct().Count() == 1)
        {
            return Result<Telefone>.Failure("Telefone deve conter DDD e 10 ou 11 dígitos válidos.");
        }

        return Result<Telefone>.Success(new Telefone(normalized));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
