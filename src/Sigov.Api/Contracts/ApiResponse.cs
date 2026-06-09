namespace Sigov.Api.Contracts;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message)
{
    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new(true, data, message);
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new(false, default, message);
    }
}
