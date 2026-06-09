namespace Sigov.Application.Common;

internal sealed record ApplicationApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    IReadOnlyCollection<string> Errors)
{
    public static ApplicationApiResponse<T> Ok(T data, string? message = null)
    {
        return new(true, data, message, Array.Empty<string>());
    }

    public static ApplicationApiResponse<T> Fail(string error)
    {
        return new(false, default, null, new[] { error });
    }

    public static ApplicationApiResponse<T> Fail(IEnumerable<string> errors)
    {
        return new(false, default, null, errors.ToArray());
    }
}
