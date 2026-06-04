using System.Text.RegularExpressions;
using Sigov.Domain.Common;

namespace Sigov.Domain.Saas;

public sealed class TenantSlug : ValueObject
{
    private static readonly Regex SlugRegex = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static Result<TenantSlug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<TenantSlug>.Failure(Error.Business("TenantSlug.Empty", "Slug do tenant é obrigatório."));
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 100 || !SlugRegex.IsMatch(normalized))
        {
            return Result<TenantSlug>.Failure(Error.Business("TenantSlug.Invalid", "Slug deve conter 3 a 100 caracteres em minúsculo, números e hífens internos."));
        }

        return Result<TenantSlug>.Success(new TenantSlug(normalized));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
