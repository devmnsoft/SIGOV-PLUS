namespace Sigov.Application.Common;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, IReadOnlyCollection<string> Errors)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message, Array.Empty<string>());

    public static ApiResponse<T> Fail(string error) => new(false, default, null, new[] { error });

    public static ApiResponse<T> Fail(IEnumerable<string> errors) => new(false, default, null, errors.ToArray());
}
