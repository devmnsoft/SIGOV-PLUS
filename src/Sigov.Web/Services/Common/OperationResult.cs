namespace Sigov.Web.Services.Common;

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Code { get; init; }
    public object? Data { get; init; }

    public static OperationResult Ok(string message, object? data = null) => new() { Success = true, Message = message, Data = data };
    public static OperationResult Fail(string message, string? code = null) => new() { Success = false, Message = message, Code = code };
}
