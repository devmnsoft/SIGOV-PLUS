namespace Sigov.Application.Common;

// Mantido como helper interno da camada Application para evitar conflito público com
// Sigov.Api.Contracts.ApiResponse<T>, que é o contrato HTTP oficial da API.
internal sealed record ApiResponse<T>(bool Success, T? Data, string? Message, IReadOnlyCollection<string> Errors)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message, Array.Empty<string>());

    public static ApiResponse<T> Fail(string error) => new(false, default, null, new[] { error });

    public static ApiResponse<T> Fail(IEnumerable<string> errors) => new(false, default, null, errors.ToArray());
}
