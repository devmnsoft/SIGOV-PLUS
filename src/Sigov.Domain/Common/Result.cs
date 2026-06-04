namespace Sigov.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error error, IReadOnlyCollection<ValidationError>? validationErrors = null)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Resultado de sucesso não pode conter erro.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Resultado de falha deve conter erro.");
        }

        IsSuccess = isSuccess;
        ErrorDetail = error;
        ValidationErrors = validationErrors ?? Array.Empty<ValidationError>();
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error ErrorDetail { get; }
    public string? Error => ErrorDetail == Error.None ? null : ErrorDetail.Message;
    public IReadOnlyCollection<ValidationError> ValidationErrors { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(string error) => new(false, Error.Business("business.error", error));

    public static Result Failure(Error error) => new(false, error);

    public static Result ValidationFailure(IEnumerable<ValidationError> validationErrors) =>
        new(false, Error.Business("validation.error", "Existem erros de validação."), validationErrors.ToArray());
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Error error, IReadOnlyCollection<ValidationError>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, Error.None);

    public new static Result<T> Failure(string error) => new(false, default, Error.Business("business.error", error));

    public new static Result<T> Failure(Error error) => new(false, default, error);

    public static Result<T> ValidationFailure(IEnumerable<ValidationError> validationErrors) =>
        new(false, default, Error.Business("validation.error", "Existem erros de validação."), validationErrors.ToArray());
}
