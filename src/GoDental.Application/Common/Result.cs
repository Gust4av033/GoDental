namespace GoDental.Application.Common;

public sealed record Result(
    bool IsSuccess,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyCollection<string>? ValidationErrors = null)
{
    public static Result Success() => new(true);

    public static Result Failure(
        string errorCode,
        string message,
        IReadOnlyCollection<string>? validationErrors = null) =>
        new(false, errorCode, message, validationErrors);
}

public sealed record Result<T>(
    bool IsSuccess,
    T? Value = default,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyCollection<string>? ValidationErrors = null)
{
    public static Result<T> Success(T value) => new(true, value);

    public static Result<T> Failure(
        string errorCode,
        string message,
        IReadOnlyCollection<string>? validationErrors = null) =>
        new(false, default, errorCode, message, validationErrors);
}
