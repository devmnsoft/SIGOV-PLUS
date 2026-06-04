using System.Net.Mail;

namespace SIGOV.Domain.Common;

public sealed class Email : ValueObject
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string value)
    {
        try
        {
            var address = new MailAddress(value ?? string.Empty);
            return address.Address == value
                ? Result<Email>.Success(new Email(value))
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
