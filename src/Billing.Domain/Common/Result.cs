namespace Billing.Domain.Common;

/// <summary>What kind of failure an <see cref="Error"/> is. Adapters map this to a transport status (e.g. HTTP).</summary>
public enum ErrorType
{
    /// <summary>The input is malformed or outside allowed limits (HTTP 400).</summary>
    Validation,

    /// <summary>The requested resource does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>The resource changed since it was read; retry with fresh data (HTTP 409).</summary>
    Conflict,

    /// <summary>The input is valid but a business rule forbids the operation in the current state (HTTP 422).</summary>
    Rule,
}

/// <summary>An expected business failure. Exceptions are reserved for programming errors.</summary>
/// <param name="Code">Stable, machine-readable code such as <c>invoice.overpayment</c>. Part of the API contract.</param>
/// <param name="Message">Human-readable explanation, safe to show to API clients.</param>
/// <param name="Type">The kind of failure; decides the HTTP status. Defaults to <see cref="ErrorType.Rule"/>.</param>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Rule);

/// <summary>Outcome of an operation that can fail for an expected reason. Check <see cref="IsSuccess"/> before continuing.</summary>
public class Result
{
    /// <summary>Creates a result. Use <see cref="Success()"/> or <see cref="Failure(Billing.Domain.Common.Error)"/> instead.</summary>
    /// <param name="error">The failure reason, or <c>null</c> for success.</param>
    protected Result(Error? error) => Error = error;

    /// <summary>The failure reason, or <c>null</c> on success.</summary>
    public Error? Error { get; }

    /// <summary><c>true</c> when the operation succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Creates a successful outcome.</summary>
    /// <returns>A result whose <see cref="IsSuccess"/> is <c>true</c>.</returns>
    public static Result Success() => new(null);

    /// <summary>Creates a failed outcome.</summary>
    /// <param name="error">Why the operation failed, usually one of the <c>InvoiceErrors</c> constants.</param>
    /// <returns>A result whose <see cref="IsSuccess"/> is <c>false</c>.</returns>
    public static Result Failure(Error error) => new(error);
}

#pragma warning disable CA1000 // Static factory members on generic type are intentional here.
/// <summary>A <see cref="Result"/> that carries a value on success.</summary>
/// <typeparam name="T">Type of the value produced on success.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>The value produced by a successful operation.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure; check <see cref="Result.IsSuccess"/> first.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    /// <summary>Creates a successful outcome carrying a value.</summary>
    /// <param name="value">The value produced by the operation.</param>
    /// <returns>A successful result whose <see cref="Value"/> is <paramref name="value"/>.</returns>
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>Creates a failed outcome. It has no value.</summary>
    /// <param name="error">Why the operation failed.</param>
    /// <returns>A failed result; reading <see cref="Value"/> on it throws.</returns>
    public static new Result<T> Failure(Error error) => new(default, error);
}
#pragma warning restore CA1000
