using DomainError = Sigov.Domain.Common.Error;

namespace Sigov.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, DomainError error, IReadOnlyCollection<ValidationError>? validationErrors = null)
    {
        if (isSuccess && error != DomainError.None)
        {
            throw new InvalidOperationException("Resultado de sucesso não pode conter erro.");
        }

        if (!isSuccess && error == DomainError.None)
        {
            throw new InvalidOperationException("Resultado de falha deve conter erro.");
        }

        IsSuccess = isSuccess;
        ErrorDetail = error;
        ValidationErrors = validationErrors ?? Array.Empty<ValidationError>();
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError ErrorDetail { get; }
    public string? Error => ErrorDetail == DomainError.None ? null : ErrorDetail.Message;
    public IReadOnlyCollection<ValidationError> ValidationErrors { get; }

    public static Result Success() => new(true, DomainError.None);

    public static Result Failure(string error) => new(false, DomainError.Business("business.error", error));

    public static Result Failure(DomainError error) => new(false, error);

    public static Result ValidationFailure(IEnumerable<ValidationError> validationErrors) =>
        new(false, DomainError.Business("validation.error", "Existem erros de validação."), validationErrors.ToArray());
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, DomainError error, IReadOnlyCollection<ValidationError>? validationErrors = null)
        : base(isSuccess, error, validationErrors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, DomainError.None);

    public new static Result<T> Failure(string error) => new(false, default, DomainError.Business("business.error", error));

    public new static Result<T> Failure(DomainError error) => new(false, default, error);

    public new static Result<T> ValidationFailure(IEnumerable<ValidationError> validationErrors) =>
        new(false, default, DomainError.Business("validation.error", "Existem erros de validação."), validationErrors.ToArray());
}
