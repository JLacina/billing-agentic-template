namespace Billing.Domain.Common;

/// <summary>An expected business failure. Exceptions are reserved for programming errors.</summary>
public sealed record Error(string Code, string Message);

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);
}

#pragma warning disable CA1000 // Static factory members on generic type are intentional here.
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value, null);
    public static new Result<T> Failure(Error error) => new(default, error);
}
#pragma warning restore CA1000
