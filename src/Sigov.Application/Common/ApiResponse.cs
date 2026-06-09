namespace Sigov.Application.Common;

// Helper interno da camada Application. O contrato HTTP oficial pertence à camada Sigov.Api.Contracts.
internal sealed record ApplicationApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    IReadOnlyCollection<string> Errors)
{
    public static ApplicationApiResponse<T> Ok(T data, string? message = null)
        => new(true, data, message, Array.Empty<string>());

    public static ApplicationApiResponse<T> Fail(string error)
        => new(false, default, null, new[] { error });

    public static ApplicationApiResponse<T> Fail(IEnumerable<string> errors)
        => new(false, default, null, errors.ToArray());
}
