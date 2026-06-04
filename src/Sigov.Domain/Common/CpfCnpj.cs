using System.Text.RegularExpressions;

namespace Sigov.Domain.Common;

public sealed class CpfCnpj : ValueObject
{
    private static readonly Regex DigitsOnly = new("\\D", RegexOptions.Compiled);

    private CpfCnpj(string value, string tipo)
    {
        Value = value;
        Tipo = tipo;
    }

    public string Value { get; }
    public string Tipo { get; }

    public static Result<CpfCnpj> Create(string? value)
    {
        var normalized = DigitsOnly.Replace(value ?? string.Empty, string.Empty);
        if (normalized.Length == 11 && Cpf.IsValid(normalized))
        {
            return Result<CpfCnpj>.Success(new CpfCnpj(normalized, "CPF"));
        }

        if (normalized.Length == 14 && Cnpj.IsValid(normalized))
        {
            return Result<CpfCnpj>.Success(new CpfCnpj(normalized, "CNPJ"));
        }

        return Result<CpfCnpj>.Failure("CPF/CNPJ inválido.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Tipo;
    }
}
