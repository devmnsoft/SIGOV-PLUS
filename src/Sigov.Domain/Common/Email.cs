using System.Net.Mail;

namespace Sigov.Domain.Common;

public sealed class Email : ValueObject
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        try
        {
            var address = new MailAddress(normalized);
            return address.Address == normalized && normalized.Contains("@", StringComparison.Ordinal)
                ? Result<Email>.Success(new Email(normalized))
                : Result<Email>.Failure("E-mail inválido.");
        }
        catch (FormatException)
        {
            return Result<Email>.Failure("E-mail inválido.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
