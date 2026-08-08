namespace Mfc.Application.Common;

/// <summary>Typed application failure used by use-case ports.</summary>
public sealed record ApplicationError(string Code, string Message)
{
    public static ApplicationError Unauthorized(string message = "Caller is not authorized for this operation.") =>
        new("unauthorized", message);

    public static ApplicationError Forbidden(string message = "Caller is forbidden from this operation.") =>
        new("forbidden", message);

    public static ApplicationError NotFound(string message) =>
        new("not_found", message);

    public static ApplicationError Conflict(string message) =>
        new("conflict", message);

    public static ApplicationError Validation(string message) =>
        new("validation", message);

    public static ApplicationError Dependency(string message) =>
        new("dependency", message);

    public static ApplicationError Failed(string message) =>
        new("failed", message);
}

/// <summary>Untyped failure carrier that converts to <see cref="ApplicationResult{T}"/>.</summary>
public readonly struct ApplicationFailure
{
    public ApplicationFailure(ApplicationError error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public ApplicationError Error { get; }
}

/// <summary>Success/failure envelope for application use cases.</summary>
/// <typeparam name="T">Success payload type.</typeparam>
public readonly struct ApplicationResult<T>
{
    private ApplicationResult(bool isSuccess, T? value, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ApplicationError? Error { get; }

    public static implicit operator ApplicationResult<T>(ApplicationFailure failure) =>
        new(false, default, failure.Error);

    internal static ApplicationResult<T> CreateSuccess(T value) =>
        new(true, value, null);
}

/// <summary>Factory helpers for <see cref="ApplicationResult{T}"/> (avoids CA1000 on the generic type).</summary>
public static class ApplicationResults
{
    /// <summary>Creates a successful result.</summary>
    public static ApplicationResult<T> Ok<T>(T value) => ApplicationResult<T>.CreateSuccess(value);

    /// <summary>Creates a failure that converts to any <see cref="ApplicationResult{T}"/>.</summary>
    public static ApplicationFailure Fail(ApplicationError error) => new(error);
}
