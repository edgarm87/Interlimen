namespace Meridian.Domain.Common;

/// <summary>
/// carries a value on success — so a login can return a result
/// giving you either the user object or an error string. This
/// is used everywhere in the auth service and application layer.
/// </summary>
/// <typeparam name="T"></typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}

/// <summary>
/// instead of throwing exceptions for expected failures (wrong password,
/// user not found, validation error), our methods return a Result object
/// that carries either a success or a failure message.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool success, string? error) { IsSuccess = success; Error = error; }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}