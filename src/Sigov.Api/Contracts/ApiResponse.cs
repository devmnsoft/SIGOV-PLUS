namespace Sigov.Api.Contracts;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, string? CorrelationId = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null, string? correlationId = null)
    {
        return new(true, data, message, correlationId);
    }

    public static ApiResponse<T> Fail(string message, string? correlationId = null)
    {
        return new(false, default, message, correlationId);
    }
}
