namespace SIGOV.Api.Contracts;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
}
